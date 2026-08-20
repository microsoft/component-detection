#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Tests;

public static class MavenTestUtilities
{
    public static string GetMalformedPomFile()
    {
        var pomFile = @"<?THISISWRONG!?>
            ";

        return pomFile;
    }

    public static string GetPomFileNoDependencies()
    {
        var pomFile = @"<?xml version=""1.0"" encoding=""UTF-8""?>
            <project xmlns=""http://maven.apache.org/POM/4.0.0"" xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xsi:schemaLocation=""http://maven.apache.org/POM/4.0.0 http://maven.apache.org/xsd/maven-4.0.0.xsd"">
                <modelVersion>4.0.0</modelVersion>
                <properties>
                    <myproperty.version>0.0.1</myproperty.version>
                </properties>

                <dependencies>
                </dependencies>
            </project>
            ";

        return pomFile;
    }

    public static string GetPomFileWithDependencyToResolveAsProperty(string groupId, string artifactId, string version)
    {
        var pomFile = @"<?xml version=""1.0"" encoding=""UTF-8""?>
            <project xmlns=""http://maven.apache.org/POM/4.0.0"" xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xsi:schemaLocation=""http://maven.apache.org/POM/4.0.0 http://maven.apache.org/xsd/maven-4.0.0.xsd"">
                <modelVersion>4.0.0</modelVersion>
                <properties>
                    <myproperty.version>{2}</myproperty.version>
                </properties>

                <dependencies>
                    <dependency>
                        <groupId>{0}</groupId>
                        <artifactId>{1}</artifactId>
                        <version>${{myproperty.version}}</version>
                    </dependency>
                </dependencies>
            </project>
            ";
        var pomFileTemplate = string.Format(pomFile, groupId, artifactId, version);
        return pomFileTemplate;
    }

    public static string GetPomFileWithDependencyToResolveAsProjectVar(string groupId, string artifactId, string version)
    {
        var pomFile = @"<?xml version=""1.0"" encoding=""UTF-8""?>
            <project xmlns=""http://maven.apache.org/POM/4.0.0"" xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xsi:schemaLocation=""http://maven.apache.org/POM/4.0.0 http://maven.apache.org/xsd/maven-4.0.0.xsd"">
                <myproperty.version>{2}</myproperty.version>
                <dependencies>
                    <dependency>
                        <groupId>{0}</groupId>
                        <artifactId>{1}</artifactId>
                        <version>${{myproperty.version}}</version>
                    </dependency>
                </dependencies>
            </project>
            ";
        var pomFileTemplate = string.Format(pomFile, groupId, artifactId, version);
        return pomFileTemplate;
    }

    public static string GetPomFileWithDependencyFailToResolve(string groupId, string artifactId, string version)
    {
        var pomFile = @"<?xml version=""1.0"" encoding=""UTF-8""?>
            <project xmlns=""http://maven.apache.org/POM/4.0.0"" xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xsi:schemaLocation=""http://maven.apache.org/POM/4.0.0 http://maven.apache.org/xsd/maven-4.0.0.xsd"">
                <modelVersion>4.0.0</modelVersion>
                <properties>
                    <myproperty.version>{2}</myproperty.version>
                </properties>

                <dependencies>
                    <dependency>
                        <groupId>{0}</groupId>
                        <artifactId>{1}</artifactId>
                        <version>${{unknown.version}}</version>
                    </dependency>
                </dependencies>
            </project>
            ";
        var pomFileTemplate = string.Format(pomFile, groupId, artifactId, version);

        return pomFileTemplate;
    }

    public static string GetPomFileWithDependency(string groupId, string artifactId, string version)
    {
        var pomFile = @"<?xml version=""1.0"" encoding=""UTF-8""?>
            <project xmlns=""http://maven.apache.org/POM/4.0.0"" xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xsi:schemaLocation=""http://maven.apache.org/POM/4.0.0 http://maven.apache.org/xsd/maven-4.0.0.xsd"">
                <modelVersion>4.0.0</modelVersion>

                <dependencies>
                    <dependency>
                        <groupId>{0}</groupId>
                        <artifactId>{1}</artifactId>
                        <version>{2}</version>
                    </dependency>
                </dependencies>
            </project>
            ";
        var pomFileTemplate = string.Format(pomFile, groupId, artifactId, version);
        return pomFileTemplate;
    }

    public static string GetPomFileWithDependencyNoVersion(string groupId, string artifactId)
    {
        var pomFile = @"<?xml version=""1.0"" encoding=""UTF-8""?>
            <project xmlns=""http://maven.apache.org/POM/4.0.0"" xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xsi:schemaLocation=""http://maven.apache.org/POM/4.0.0 http://maven.apache.org/xsd/maven-4.0.0.xsd"">
                <modelVersion>4.0.0</modelVersion>

                <dependencies>
                    <dependency>
                        <groupId>{0}</groupId>
                        <artifactId>{1}</artifactId>
                    </dependency>
                </dependencies>
            </project>
            ";

        var pomFileTemplate = string.Format(pomFile, groupId, artifactId);
        return pomFileTemplate;
    }
}
