import java.io.File;
import java.io.FileNotFoundException;
import java.io.FileOutputStream;
import java.io.PrintStream;

import java.util.ArrayList;
import java.util.Arrays;
import java.util.HashMap;
import java.util.HashSet;
import java.util.List;
import java.util.Map;
import java.util.Set;

import org.apache.ivy.ant.IvyPostResolveTask;
import org.apache.ivy.core.module.id.ModuleRevisionId;
import org.apache.ivy.core.report.ResolveReport;
import org.apache.ivy.core.report.ArtifactDownloadReport;
import org.apache.ivy.core.resolve.IvyNode;
import org.apache.ivy.core.resolve.IvyNodeCallers.Caller;
import org.apache.ivy.core.resolve.IvyNodeEviction.EvictionData;
import org.apache.tools.ant.BuildException;

/**
 * An Ant task to write out RegisterUsage.json, which is a list of instructions for passing to RegisterUsage.
 *
 * Dependencies specified with org+name+rev, with org different from the project org are assumed to be Maven dependencies
 * published to a public Maven repository with GAV == (groupId, artifactId, version) == (org, name, rev).  Other components
 * are ignored.
 *
 * If the ivy.xml defines a configuration called "default" or "runtime", any dependency required in that configuration
 * is assumed to be a runtime dependency, and the rest are assumed to be dev dependencies.  Otherwise, all dependencies
 * are assumed to be non-dev dependencies.
 *
 * This task is based on Ivy source code from here:
 * https://github.com/apache/ant-ivy/blob/master/src/java/org/apache/ivy/ant/IvyDependencyTree.java
 *
 * JSON output is constructed directly to avoid adding an extra dependency for a JSON library.
 */
public final class IvyComponentDetectionAntTask extends IvyPostResolveTask {

    private static final String[] RUNTIME_CONF_NAMES = {"default", "runtime"};

    /* Parameters passed via build.xml */
    private File outFile = null;

    /* Internal state fields */
    private final Map<ModuleRevisionId, List<CGNode>> dependencies = new HashMap<>();
    private boolean detectDevDependencies = false;

    @Override
    public void doExecute() throws BuildException {
        prepareAndCheck();
        final ResolveReport report = getResolvedReport();
        if (report == null) {
            throw new BuildException("No resolution report was available to run the post-resolve task. Make sure resolve was done before this task");
        }
        log("Component Detection for " + report.getResolveId());
        final ModuleRevisionId rootMrid = report.getModuleDescriptor().getModuleRevisionId();
        final String rootOrganisation = rootMrid.getOrganisation();
        log("Root organisation is " + rootOrganisation + ".  Dependencies with this groupId will be ignored.");
        final String[] allConfigurations = report.getConfigurations();
        log("All configurations: " + String.join(", ", report.getConfigurations()));
        for (final String runtimeConfigurationName : RUNTIME_CONF_NAMES) {
            if (Arrays.stream(allConfigurations).anyMatch(runtimeConfigurationName::equals)) {
                this.detectDevDependencies = true;
                log("Detected configuration with name '" + runtimeConfigurationName + "' in ivy.xml.  Activating dev dependency detection: all dependencies not required for " + String.join(" or ", RUNTIME_CONF_NAMES) + " will be marked as dev dependencies.");
            }
        }
        final IvyNode[] unresolvedDeps = report.getUnresolvedDependencies();
        if (unresolvedDeps != null) for (final IvyNode unresolvedDep : unresolvedDeps) {
            if (unresolvedDep.getId().getOrganisation() != rootOrganisation) {
                log("Warning: dependency could not be resolved and will not be passed to Component Governance: " + unresolvedDep.getId());
            }
        }
        if (!this.detectDevDependencies) {
            log("Warning: will not discriminate between dev dependencies and runtime dependencies, because ivy.xml defines no configurations called " + String.join(" or ", RUNTIME_CONF_NAMES));
        }
        for (final IvyNode dependency : report.getDependencies()) {
            if (dependency.getId().getOrganisation() == rootOrganisation) {
                log("Warning: direct dependency " + dependency.getId() + " has organisation " + rootOrganisation + " and so is considered to be custom code.  If it is really a third-party dependency published to Maven Central, please update its org/name/rev attributes to match its Maven GAV.");
            }
            if (dependency.isCompletelyEvicted()) {
                log("Ignoring evicted dependency " + dependency.getId());
            }
            populateDependencyTree(dependency);
        }
        final List<CGNode> dependencyList = this.dependencies.get(rootMrid);
        if (dependencyList != null) {
            log("Writing output to " + this.outFile.getAbsolutePath());
            try (final PrintStream outStream = new PrintStream(new FileOutputStream(this.outFile))) {
                writeRegisterUsage(outStream, dependencyList, rootMrid);
            } catch (final FileNotFoundException e) {
                throw new BuildException("Failed to write cgmanifest.json", e);
            }
        }
    }

    /**
     * Flatten the list of unique dependencies.
     */
    private void flattenDependencyListRecursive(final List<CGNode> listIn, final List<CGNode> listOut, final Set<ModuleRevisionId> dedup, final String rootOrganisation) {
        for (final CGNode node : listIn) {
            final ModuleRevisionId mrid = stripExtraAttributes(node.getIvyNode().getId());
            if (mrid.getOrganisation() != rootOrganisation && dedup.add(mrid)) {
                listOut.add(node);
                final List<CGNode> dependenciesForModule = this.dependencies.get(mrid);
                if (dependenciesForModule != null && !dependenciesForModule.isEmpty()) {
                    flattenDependencyListRecursive(dependenciesForModule, listOut, dedup, rootOrganisation);
                }
            }
        }
    }

    /**
     * Write the output file.
     */
    private void writeRegisterUsage(final PrintStream out, final List<CGNode> baseDependencyList, final ModuleRevisionId rootMrid) {
        final List<CGNode> flattenedDependencyList = new ArrayList<>();
        flattenDependencyListRecursive(baseDependencyList, flattenedDependencyList, new HashSet<ModuleRevisionId>(), rootMrid.getOrganisation());
        out.println("{\"RegisterUsage\": [");
        boolean needComma = false;
        for (final CGNode node : flattenedDependencyList) {
            final IvyNode dependency = node.getIvyNode();
            final ModuleRevisionId parentMrid = node.getParentMrid();
            final boolean isDevDependency = node.isDevDependency();
            final ModuleRevisionId mrid = stripExtraAttributes(dependency.getId());
            if (needComma) {
                out.println(",");
            }
            out.print("{\"gav\": ");
            out.print(jsonGav(mrid));
            out.print(", \"DevelopmentDependency\": ");
            out.print(jsonBoolean(isDevDependency));
            out.print(", \"resolved\": ");
            out.print(jsonBoolean(!dependency.hasProblem()));
            if (parentMrid != null && parentMrid != rootMrid) {
                out.print(", \"parent_gav\": ");
                out.print(jsonGav(parentMrid));
            }
            out.print("}");
            needComma = true;
        }
        out.println("\n]\n}");
    }

    /**
     * Quote and escape a JSON string literal.
     */
    private static String jsonStringLiteral(final String s) {
        if (s == null) {
            return "null";
        }
        final StringBuilder ret = new StringBuilder("\"");
        final int len = s.length();
        for (int charOffset = 0; charOffset < len; ) {
            final int codepoint = s.codePointAt(charOffset);
            if (codepoint == '\\') {
                ret.append("\\\\");
            } else if (codepoint == '"') {
                ret.append("\\\"");
            } else if (codepoint == 10) {
                ret.append("\\n");
            } else if (codepoint == 13) {
                ret.append("\\r");
            } else if (codepoint < 32) {
                ret.append(String.format("\\u%04x", codepoint));
            } else {
                ret.appendCodePoint(codepoint);
            }
            charOffset += Character.charCount(codepoint);
        }
        ret.append("\"");
        return ret.toString();
    }

    private static String jsonBoolean(final boolean value) {
        return value ? "true" : "false";
    }

    /**
     * Serialise JSON representing a GAV {"g": <groupId>, "a": <artifactId>, "v": <version>}
     * @param mrid Ivy dependency ID object.
     * @return String representing mrid as JSON.
     */
    private static String jsonGav(final ModuleRevisionId mrid) {
        final StringBuilder ret = new StringBuilder("{\"g\": ");
        ret.append(jsonStringLiteral(mrid.getOrganisation()));
        ret.append(", \"a\": ");
        ret.append(jsonStringLiteral(mrid.getName()));
        ret.append(", \"v\": ");
        ret.append(jsonStringLiteral(mrid.getRevision()));
        ret.append("}");
        return ret.toString();
    }

    /**
     * Extra attributes are in the ModuleRevisionId's returned by ResolveReport.getDependencies() but not those returned by
     * dependency.getAllCallers().  So strip them so that the ModuleRevisionId's match up.
     */
    private static ModuleRevisionId stripExtraAttributes(final ModuleRevisionId mrid) {
        final Map<String, String> extraAttributes = mrid.getExtraAttributes();
        if (extraAttributes.isEmpty()) {
            return mrid;
        } else {
            return ModuleRevisionId.newInstance(mrid.getOrganisation(), mrid.getName(), mrid.getBranch(), mrid.getRevision());
        }
    }

    /**
     * Check whether a dependency is a dev dependency.  If dev dependency detection is enabled, any dependency not required in
     * any of the RUNTIME_CONF_NAMES configurations is marked as a dev dependency.
     */
    private boolean checkIsDevDependency(final IvyNode dependency) {
        if (this.detectDevDependencies) {
            for (final String nondevConfName : RUNTIME_CONF_NAMES) {
                final String[] depConfs = dependency.getConfigurations(nondevConfName);
                if (depConfs != null && depConfs.length > 0) {
                    return false;
                }
            }
            log("Marking dependency " + dependency.getId() + " as a dev dependency because it's not required by configurations " + String.join(", ", RUNTIME_CONF_NAMES));
            return true;
        } else {
            return false;
        }
    }

    /**
     * Build the dependency tree from the given root.
     */
    private void populateDependencyTree(final IvyNode dependency) {
        registerNodeIfNecessary(stripExtraAttributes(dependency.getId()));
        final Set<ModuleRevisionId> dedup = new HashSet<ModuleRevisionId>();
        final boolean isDevDependency = checkIsDevDependency(dependency);
        for (final Caller caller : dependency.getAllCallers()) {
            // stripExtraAttributes in the next line is redundant in Ivy v2.5.0, but included in case future versions of Ivy
            // include the extraAttributes in caller.getModuleRevisionId().
            final ModuleRevisionId mrid = stripExtraAttributes(caller.getModuleRevisionId());
            if (dedup.add(dependency.getId())) {
                if (dependency.isCompletelyEvicted()) {
                    log("Ignoring evicted dependency " + dependency.getId() + " (transitive dependency of " + mrid + ")");
                } else {
                    log("Dependency " + mrid + " has transitive dependency " + dependency.getId());
                    addDependency(mrid, dependency, isDevDependency);
                }
            }
        }
    }

    /**
     * Add a new entry to the this.dependencies if needed.
     */
    private void registerNodeIfNecessary(final ModuleRevisionId moduleRevisionId) {
        if (!this.dependencies.containsKey(moduleRevisionId)) {
            this.dependencies.put(moduleRevisionId, new ArrayList<CGNode>());
        }
    }

    /**
     * Register a new dependency of a given node.
     */
    private void addDependency(final ModuleRevisionId parentMrid, final IvyNode childDependency, final boolean isDevDependency) {
        final ModuleRevisionId parentMridStripped = stripExtraAttributes(parentMrid);
        registerNodeIfNecessary(parentMridStripped);
        final CGNode newNode = new CGNode(childDependency, parentMridStripped, isDevDependency);
        this.dependencies.get(parentMridStripped).add(newNode);
    }

    /**
     * Set task argument.
     * @param outFile Desired output file.
     */
    public void setOut(final File outFile) {
        this.outFile = outFile;
    }

    /**
     * Class to represent an entry in the dependency tree.  It stores the underlying Ivy node and the
     * dev/non-dev metadata, and also allows them to be sorted.
     */
    private static final class CGNode implements Comparable<CGNode> {
        private final IvyNode ivyNode;
        private final ModuleRevisionId parentMrid;
        private final boolean devDependency;

        public CGNode(final IvyNode ivyNode, final ModuleRevisionId parentMrid, final boolean devDependency) {
            this.ivyNode = ivyNode;
            this.parentMrid = parentMrid;
            this.devDependency = devDependency;
        }

        public IvyNode getIvyNode() {
            return this.ivyNode;
        }

        public ModuleRevisionId getParentMrid() {
            return this.parentMrid;
        }

        public boolean isDevDependency() {
            return this.devDependency;
        }

        @Override
        public int compareTo(final CGNode other) {
            final ModuleRevisionId myMrid = this.ivyNode.getId();
            final ModuleRevisionId theirMrid = other.ivyNode.getId();
            int ret = myMrid.getOrganisation().compareTo(theirMrid.getOrganisation());
            if (ret != 0) {
                return ret;
            }
            ret = myMrid.getName().compareTo(theirMrid.getName());
            if (ret != 0) {
                return ret;
            }
            ret = myMrid.getRevision().compareTo(theirMrid.getRevision());
            if (ret != 0) {
                return ret;
            }
            return myMrid.toString().compareTo(theirMrid.toString());
        }
    }
}
