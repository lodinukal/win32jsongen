﻿// <copyright file="TypeGenInfo.cs" company="https://github.com/marlersoft">
// Copyright (c) https://github.com/marlersoft. All rights reserved.
// </copyright>

namespace JsonWin32Generator
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection.Metadata;

    internal class TypeGenInfo
    {
        private List<TypeGenInfo>? nestedTypes;

        private TypeGenInfo(TypeDefinition def, string apiName, string name, string apiNamespace, string fqn, TypeGenInfo? enclosingType)
        {
            this.Def = def;
            this.ApiName = apiName;
            this.Name = name;
            this.ApiNamespace = apiNamespace;
            this.Fqn = fqn;
            this.EnclosingType = enclosingType;
        }

        internal TypeDefinition Def { get; }

        internal string ApiName { get; }

        internal string Name { get; }

        internal string ApiNamespace { get; }

        internal string Fqn { get; } // note: all fqn (fully qualified name)'s are unique

        internal TypeGenInfo? EnclosingType { get; }

        internal bool IsCom { get { return this.Def.BaseType.IsNil; } }

        internal bool IsNested
        {
            get
            {
                return this.EnclosingType != null;
            }
        }

        internal uint NestedTypeCount
        {
            get
            {
                return (this.nestedTypes == null) ? 0 : (uint)this.nestedTypes.Count;
            }
        }

        internal IEnumerable<TypeGenInfo> NestedTypesEnumerable
        {
            get
            {
                return (this.nestedTypes == null) ? Enumerable.Empty<TypeGenInfo>() : this.nestedTypes;
            }
        }

        internal static TypeGenInfo CreateNotNested(TypeDefinition def, string name, string @namespace, Dictionary<string, string> apiNamespaceToName)
        {
            Enforce.Invariant(!def.IsNested, "CreateNotNested called for TypeDefinition that is nested");
            string? apiName;
            if (!apiNamespaceToName.TryGetValue(@namespace, out apiName))
            {
                Enforce.Data(@namespace.StartsWith(Metadata.WindowsWin32NamespacePrefix, StringComparison.Ordinal));
                apiName = @namespace.Substring(Metadata.WindowsWin32NamespacePrefix.Length);
                apiNamespaceToName.Add(@namespace, apiName);
            }

            string fqn = Fmt.In($"{@namespace}.{name}");
            return new TypeGenInfo(
                def: def,
                apiName: apiName,
                name: name,
                apiNamespace: @namespace,
                fqn: fqn,
                enclosingType: null);
        }

        internal static TypeGenInfo CreateNested(MetadataReader mr, TypeDefinition def, TypeGenInfo enclosingType)
        {
            Enforce.Invariant(def.IsNested, "CreateNested called for TypeDefinition that is not nested");
            string name = mr.GetString(def.Name);
            string @namespace = mr.GetString(def.Namespace);
            Enforce.Data(@namespace.Length == 0, "I thought all nested types had an empty namespace");
            string fqn = Fmt.In($"{enclosingType.Fqn}+{name}");
            return new TypeGenInfo(
                def: def,
                apiName: enclosingType.ApiName,
                name: name,
                apiNamespace: enclosingType.ApiNamespace,
                fqn: fqn,
                enclosingType: enclosingType);
        }

        internal TypeGenInfo? TryGetNestedTypeByName(string name)
        {
            if (this.nestedTypes != null)
            {
                foreach (TypeGenInfo info in this.nestedTypes)
                {
                    if (info.Name == name)
                    {
                        return info;
                    }
                }
            }

            return null;
        }

        internal void AddNestedType(TypeGenInfo type_info)
        {
            if (this.nestedTypes == null)
            {
                this.nestedTypes = new List<TypeGenInfo>();
            }
            else if (this.TryGetNestedTypeByName(type_info.Name) != null)
            {
                throw new InvalidOperationException(Fmt.In($"nested type '{type_info.Name}' already exists in '{this.Fqn}'"));
            }

            this.nestedTypes.Add(type_info);
        }

        internal TypeGenInfo GetNestedTypeByName(string name) => this.TryGetNestedTypeByName(name) is TypeGenInfo info ? info :
                throw new ArgumentException(Fmt.In($"type '{this.Fqn}' does not have nested type '{name}'"));

        internal bool HasNestedType(TypeGenInfo typeInfo)
        {
            Enforce.Invariant(typeInfo.IsNested);
            foreach (TypeGenInfo nestedTypeInfo in this.NestedTypesEnumerable)
            {
                if (object.ReferenceEquals(nestedTypeInfo, typeInfo))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
