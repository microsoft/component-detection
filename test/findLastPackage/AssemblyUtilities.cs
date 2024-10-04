using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;

namespace findLastPackage
{
    internal static class AssemblyUtilities
    {

        internal static (Version assemblyVersion, Version fileVersion) GetVersions(Stream assemblyStream)
        {
            using (var peReader = new PEReader(assemblyStream))
            {
                var metadataReader = peReader.GetMetadataReader();
                var assemblyDefinition = metadataReader.GetAssemblyDefinition();
                var name = metadataReader.GetString(assemblyDefinition.Name);
                var version = assemblyDefinition.Version;

                string? fileVersion = null;
                MetadataStringComparer comparer = metadataReader.StringComparer;
                foreach (CustomAttributeHandle attrHandle in assemblyDefinition.GetCustomAttributes())
                {
                    CustomAttribute attr = metadataReader.GetCustomAttribute(attrHandle);
                    StringHandle typeNamespaceHandle = default(StringHandle), typeNameHandle = default(StringHandle);
                    if (TryGetAttributeName(metadataReader, attr, out typeNamespaceHandle, out typeNameHandle) &&
                        comparer.Equals(typeNamespaceHandle, "System.Reflection"))
                    {
                        if (comparer.Equals(typeNameHandle, "AssemblyFileVersionAttribute"))
                        {
                            GetStringAttributeArgumentValue(metadataReader, attr, ref fileVersion);
                        }
                    }
                }

                return (assemblyVersion: version, fileVersion: Version.Parse(fileVersion));
            }
        }



        /// <summary>Gets the name of an attribute.</summary>
        /// <param name="reader">The metadata reader.</param>
        /// <param name="attr">The attribute.</param>
        /// <param name="typeNamespaceHandle">The namespace of the attribute.</param>
        /// <param name="typeNameHandle">The name of the attribute.</param>
        /// <returns>true if the name could be retrieved; otherwise, false.</returns>
        private static bool TryGetAttributeName(MetadataReader reader, CustomAttribute attr, out StringHandle typeNamespaceHandle, out StringHandle typeNameHandle)
        {
            EntityHandle ctorHandle = attr.Constructor;
            switch (ctorHandle.Kind)
            {
                case HandleKind.MemberReference:
                    EntityHandle container = reader.GetMemberReference((MemberReferenceHandle)ctorHandle).Parent;
                    if (container.Kind == HandleKind.TypeReference)
                    {
                        TypeReference tr = reader.GetTypeReference((TypeReferenceHandle)container);
                        typeNamespaceHandle = tr.Namespace;
                        typeNameHandle = tr.Name;
                        return true;
                    }
                    break;

                case HandleKind.MethodDefinition:
                    MethodDefinition md = reader.GetMethodDefinition((MethodDefinitionHandle)ctorHandle);
                    TypeDefinition td = reader.GetTypeDefinition(md.GetDeclaringType());
                    typeNamespaceHandle = td.Namespace;
                    typeNameHandle = td.Name;
                    return true;
            }

            // Unusual case, potentially invalid IL
            typeNamespaceHandle = default(StringHandle);
            typeNameHandle = default(StringHandle);
            return false;
        }

        /// <summary>Gets the string argument value of an attribute with a single fixed string argument.</summary>
        /// <param name="reader">The metadata reader.</param>
        /// <param name="attr">The attribute.</param>
        /// <param name="value">The value parsed from the attribute, if it could be retrieved; otherwise, the value is left unmodified.</param>
        private static void GetStringAttributeArgumentValue(MetadataReader reader, CustomAttribute attr, ref string? value)
        {
            EntityHandle ctorHandle = attr.Constructor;
            BlobHandle signature;
            switch (ctorHandle.Kind)
            {
                case HandleKind.MemberReference:
                    signature = reader.GetMemberReference((MemberReferenceHandle)ctorHandle).Signature;
                    break;
                case HandleKind.MethodDefinition:
                    signature = reader.GetMethodDefinition((MethodDefinitionHandle)ctorHandle).Signature;
                    break;
                default:
                    // Unusual case, potentially invalid IL
                    return;
            }

            BlobReader signatureReader = reader.GetBlobReader(signature);
            BlobReader valueReader = reader.GetBlobReader(attr.Value);

            const ushort Prolog = 1; // two-byte "prolog" defined by ECMA-335 (II.23.3) to be at the beginning of attribute value blobs
            if (valueReader.ReadUInt16() == Prolog)
            {
                SignatureHeader header = signatureReader.ReadSignatureHeader();
                int parameterCount;
                if (header.Kind == SignatureKind.Method &&                               // attr ctor must be a method
                    !header.IsGeneric &&                                                 // attr ctor must be non-generic
                    signatureReader.TryReadCompressedInteger(out parameterCount) &&      // read parameter count
                    parameterCount == 1 &&                                               // attr ctor must have 1 parameter
                    signatureReader.ReadSignatureTypeCode() == SignatureTypeCode.Void && // attr ctor return type must be void
                    signatureReader.ReadSignatureTypeCode() == SignatureTypeCode.String) // attr ctor first parameter must be string
                {
                    value = valueReader.ReadSerializedString();
                }
            }
        }

    }
}
