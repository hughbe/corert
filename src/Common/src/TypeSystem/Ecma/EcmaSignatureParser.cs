// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Diagnostics;

using Internal.TypeSystem;

namespace Internal.TypeSystem.Ecma
{
    public struct EcmaSignatureParser
    {
        private EcmaModule _module;
        private BlobReader _reader;

        // TODO
        // bool _hasModifiers;

        public EcmaSignatureParser(EcmaModule module, BlobReader reader)
        {
            _module = module;
            _reader = reader;
        }

        private TypeDesc GetWellKnownType(WellKnownType wellKnownType)
        {
            return _module.Context.GetWellKnownType(wellKnownType);
        }

        private TypeDesc ParseType(SignatureTypeCode typeCode)
        {
            // Switch on the type.
            switch (typeCode)
            {
                case SignatureTypeCode.Void:
                    return GetWellKnownType(WellKnownType.Void);
                case SignatureTypeCode.Boolean:
                    return GetWellKnownType(WellKnownType.Boolean);
                case SignatureTypeCode.SByte:
                    return GetWellKnownType(WellKnownType.SByte);
                case SignatureTypeCode.Byte:
                    return GetWellKnownType(WellKnownType.Byte);
                case SignatureTypeCode.Int16:
                    return GetWellKnownType(WellKnownType.Int16);
                case SignatureTypeCode.UInt16:
                    return GetWellKnownType(WellKnownType.UInt16);
                case SignatureTypeCode.Int32:
                    return GetWellKnownType(WellKnownType.Int32);
                case SignatureTypeCode.UInt32:
                    return GetWellKnownType(WellKnownType.UInt32);
                case SignatureTypeCode.Int64:
                    return GetWellKnownType(WellKnownType.Int64);
                case SignatureTypeCode.UInt64:
                    return GetWellKnownType(WellKnownType.UInt64);
                case SignatureTypeCode.Single:
                    return GetWellKnownType(WellKnownType.Single);
                case SignatureTypeCode.Double:
                    return GetWellKnownType(WellKnownType.Double);
                case SignatureTypeCode.Char:
                    return GetWellKnownType(WellKnownType.Char);
                case SignatureTypeCode.String:
                    return GetWellKnownType(WellKnownType.String);
                case SignatureTypeCode.IntPtr:
                    return GetWellKnownType(WellKnownType.IntPtr);
                case SignatureTypeCode.UIntPtr:
                    return GetWellKnownType(WellKnownType.UIntPtr);
                case SignatureTypeCode.Object:
                    return GetWellKnownType(WellKnownType.Object);
                case SignatureTypeCode.TypeHandle:
                    return _module.GetType(_reader.ReadTypeHandle());
                case SignatureTypeCode.SZArray:
                    return _module.Context.GetArrayType(ParseType());
                case SignatureTypeCode.Array:
                    {
                        var elementType = ParseType();
                        var rank = _reader.ReadCompressedInteger();

                        // TODO: Bounds for multi-dimmensional arrays
                        var boundsCount = _reader.ReadCompressedInteger();
                        for (int i = 0; i < boundsCount; i++)
                            _reader.ReadCompressedInteger();
                        var lowerBoundsCount = _reader.ReadCompressedInteger();
                        for (int j = 0; j < lowerBoundsCount; j++)
                            _reader.ReadCompressedInteger();

                        return _module.Context.GetArrayType(elementType, rank);
                    }
                case SignatureTypeCode.ByReference:
                    return ParseType().MakeByRefType();
                case SignatureTypeCode.Pointer:
                    return _module.Context.GetPointerType(ParseType());
                case SignatureTypeCode.GenericTypeParameter:
                    return _module.Context.GetSignatureVariable(_reader.ReadCompressedInteger(), false);
                case SignatureTypeCode.GenericMethodParameter:
                    return _module.Context.GetSignatureVariable(_reader.ReadCompressedInteger(), true);
                case SignatureTypeCode.GenericTypeInstance:
                    {
                        TypeDesc typeDef = ParseType();
                        MetadataType metadataTypeDef = typeDef as MetadataType;
                        if (metadataTypeDef == null)
                            throw new BadImageFormatException();

                        TypeDesc[] instance = new TypeDesc[_reader.ReadCompressedInteger()];
                        for (int i = 0; i < instance.Length; i++)
                            instance[i] = ParseType();
                        return _module.Context.GetInstantiatedType(metadataTypeDef, new Instantiation(instance));
                    }
                case SignatureTypeCode.TypedReference:
                    throw new PlatformNotSupportedException("TypedReference not supported in .NET Core");
                case SignatureTypeCode.FunctionPointer:
                    return _module.Context.GetFunctionPointerType(ParseMethodSignature());
                default:
                    throw new BadImageFormatException();
            }
        }

        private SignatureTypeCode ParseTypeCode(bool skipPinned = true)
        {
            for (;;)
            {
                SignatureTypeCode typeCode = _reader.ReadSignatureTypeCode();

                // TODO: actually consume modopts
                if (typeCode == SignatureTypeCode.RequiredModifier ||
                    typeCode == SignatureTypeCode.OptionalModifier)
                {
                    _reader.ReadTypeHandle();
                    continue;
                }

                // TODO: treat PINNED in the signature same as modopts (it matters
                // in signature matching - you can actually define overloads on this)
                if (skipPinned && typeCode == SignatureTypeCode.Pinned)
                {
                    continue;
                }

                return typeCode;
            }
        }

        public TypeDesc ParseType()
        {
            return ParseType(ParseTypeCode());
        }

        public bool IsFieldSignature
        {
            get
            {
                BlobReader peek = _reader;
                return peek.ReadSignatureHeader().Kind == SignatureKind.Field;
            }
        }

        public MethodSignature ParseMethodSignature()
        {
            SignatureHeader header = _reader.ReadSignatureHeader();

            MethodSignatureFlags flags = 0;

            SignatureCallingConvention signatureCallConv = header.CallingConvention;
            if (signatureCallConv != SignatureCallingConvention.Default)
            {
                // Verify that it is safe to convert CallingConvention to MethodSignatureFlags via a simple cast
                Debug.Assert((int)MethodSignatureFlags.UnmanagedCallingConventionCdecl == (int)SignatureCallingConvention.CDecl);
                Debug.Assert((int)MethodSignatureFlags.UnmanagedCallingConventionStdCall == (int)SignatureCallingConvention.StdCall);
                Debug.Assert((int)MethodSignatureFlags.UnmanagedCallingConventionThisCall == (int)SignatureCallingConvention.ThisCall);

                flags = (MethodSignatureFlags)signatureCallConv;
            }

            if (!header.IsInstance)
                flags |= MethodSignatureFlags.Static;

            int arity = header.IsGeneric ? _reader.ReadCompressedInteger() : 0;

            int count = _reader.ReadCompressedInteger();

            TypeDesc returnType = ParseType();
            TypeDesc[] parameters;

            if (count > 0)
            {
                // Get all of the parameters.
                parameters = new TypeDesc[count];
                for (int i = 0; i < count; i++)
                {
                    parameters[i] = ParseType();
                }
            }
            else
            {
                parameters = TypeDesc.EmptyTypes;
            }

            return new MethodSignature(flags, arity, returnType, parameters);
        }

        public PropertySignature ParsePropertySignature()
        {
            SignatureHeader header = _reader.ReadSignatureHeader();
            if (header.Kind != SignatureKind.Property)
                throw new BadImageFormatException();

            bool isStatic = !header.IsInstance;

            int count = _reader.ReadCompressedInteger();

            TypeDesc returnType = ParseType();
            TypeDesc[] parameters;

            if (count > 0)
            {
                // Get all of the parameters.
                parameters = new TypeDesc[count];
                for (int i = 0; i < count; i++)
                {
                    parameters[i] = ParseType();
                }
            }
            else
            {
                parameters = TypeDesc.EmptyTypes;
            }

            return new PropertySignature(isStatic, parameters, returnType);
        }

        public TypeDesc ParseFieldSignature()
        {
            if (_reader.ReadSignatureHeader().Kind != SignatureKind.Field)
                throw new BadImageFormatException();

            return ParseType();
        }

        public LocalVariableDefinition[] ParseLocalsSignature()
        {
            if (_reader.ReadSignatureHeader().Kind != SignatureKind.LocalVariables)
                throw new BadImageFormatException();

            int count = _reader.ReadCompressedInteger();

            LocalVariableDefinition[] locals;

            if (count > 0)
            {
                locals = new LocalVariableDefinition[count];
                for (int i = 0; i < count; i++)
                {
                    bool isPinned = false;

                    SignatureTypeCode typeCode = ParseTypeCode(skipPinned: false);
                    if (typeCode == SignatureTypeCode.Pinned)
                    {
                        isPinned = true;
                        typeCode = ParseTypeCode();
                    }

                    locals[i] = new LocalVariableDefinition(ParseType(typeCode), isPinned);
                }
            }
            else
            {
                locals = Array.Empty<LocalVariableDefinition>();
            }
            return locals;
        }

        public TypeDesc[] ParseMethodSpecSignature()
        {
            if (_reader.ReadSignatureHeader().Kind != SignatureKind.MethodSpecification)
                throw new BadImageFormatException();

            int count = _reader.ReadCompressedInteger();

            if (count <= 0)
                throw new BadImageFormatException();

            TypeDesc[] arguments = new TypeDesc[count];
            for (int i = 0; i < count; i++)
            {
                arguments[i] = ParseType();
            }
            return arguments;
        }
    }
}
