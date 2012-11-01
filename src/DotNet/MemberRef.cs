﻿using System;
using System.Collections.Generic;
using dot10.DotNet.MD;

namespace dot10.DotNet {
	/// <summary>
	/// A high-level representation of a row in the MemberRef table
	/// </summary>
	public abstract class MemberRef : IHasCustomAttribute, IMethodDefOrRef, ICustomAttributeType, IField {
		/// <summary>
		/// The row id in its table
		/// </summary>
		protected uint rid;

		/// <summary>
		/// The owner module
		/// </summary>
		protected ModuleDef ownerModule;

		/// <inheritdoc/>
		public MDToken MDToken {
			get { return new MDToken(Table.MemberRef, rid); }
		}

		/// <inheritdoc/>
		public uint Rid {
			get { return rid; }
			set { rid = value; }
		}

		/// <inheritdoc/>
		public int HasCustomAttributeTag {
			get { return 6; }
		}

		/// <inheritdoc/>
		public int MethodDefOrRefTag {
			get { return 1; }
		}

		/// <inheritdoc/>
		public int CustomAttributeTypeTag {
			get { return 3; }
		}

		/// <summary>
		/// From column MemberRef.Class
		/// </summary>
		public abstract IMemberRefParent Class { get; set; }

		/// <summary>
		/// From column MemberRef.Name
		/// </summary>
		public abstract UTF8String Name { get; set; }

		/// <summary>
		/// From column MemberRef.Signature
		/// </summary>
		public abstract CallingConventionSig Signature { get; set; }

		/// <summary>
		/// Gets all custom attributes
		/// </summary>
		public abstract CustomAttributeCollection CustomAttributes { get; }

		/// <inheritdoc/>
		public ITypeDefOrRef DeclaringType {
			get {
				var owner = Class;

				var tdr = owner as ITypeDefOrRef;
				if (tdr != null)
					return tdr;

				var method = owner as MethodDef;
				if (method != null)
					return method.DeclaringType;

				var mr = owner as ModuleRef;
				if (mr != null) {
					//TODO: Use the correct namespace + name of the global type in the referenced module
					var tr = new TypeRefUser(ownerModule, "", "<Module>", mr);
					if (ownerModule != null)
						return ownerModule.UpdateRowId(tr);
					return tr;
				}

				return null;
			}
		}

		/// <summary>
		/// <c>true</c> if this is a method reference (<see cref="MethodSig"/> != <c>null</c>)
		/// </summary>
		public bool IsMethodRef {
			get { return MethodSig != null; }
		}

		/// <summary>
		/// <c>true</c> if this is a field reference (<see cref="FieldSig"/> != <c>null</c>)
		/// </summary>
		public bool IsFieldRef {
			get { return FieldSig != null; }
		}

		/// <summary>
		/// Gets/sets the method sig
		/// </summary>
		public MethodSig MethodSig {
			get { return Signature as MethodSig; }
			set { Signature = value; }
		}

		/// <summary>
		/// Gets/sets the field sig
		/// </summary>
		public FieldSig FieldSig {
			get { return Signature as FieldSig; }
			set { Signature = value; }
		}

		/// <inheritdoc/>
		public ModuleDef OwnerModule {
			get { return ownerModule; }
		}

		/// <inheritdoc/>
		bool IGenericParameterProvider.IsMethod {
			get { return true; }
		}

		/// <inheritdoc/>
		bool IGenericParameterProvider.IsType {
			get { return false; }
		}

		/// <inheritdoc/>
		int IGenericParameterProvider.NumberOfGenericParameters {
			get {
				var sig = MethodSig;
				return sig == null ? 0 : (int)sig.GenParamCount;
			}
		}

		/// <summary>
		/// Gets the full name
		/// </summary>
		public string FullName {
			get {
				var parent = Class;
				IList<TypeSig> typeGenArgs = null;
				if (parent is TypeSpec) {
					var sig = ((TypeSpec)parent).TypeSig as GenericInstSig;
					if (sig != null)
						typeGenArgs = sig.GenericArguments;
				}
				if (IsMethodRef)
					return FullNameCreator.MethodFullName(GetDeclaringTypeFullName(), Name, MethodSig, typeGenArgs, null);
				if (IsFieldRef)
					return FullNameCreator.FieldFullName(GetDeclaringTypeFullName(), Name, FieldSig, typeGenArgs);
				return string.Empty;
			}
		}

		/// <summary>
		/// Get the declaring type's full name
		/// </summary>
		/// <returns>Full name or <c>null</c> if there's no declaring type</returns>
		public string GetDeclaringTypeFullName() {
			var parent = Class;
			if (parent == null)
				return null;
			if (parent is ITypeDefOrRef)
				return ((ITypeDefOrRef)parent).FullName;
			if (parent is ModuleRef)
				return string.Format("[module:{0}]<Module>", ((ModuleRef)parent).ToString());
			if (parent is MethodDef) {
				var declaringType = ((MethodDef)parent).DeclaringType;
				return declaringType == null ? null : declaringType.FullName;
			}
			return null;	// Should never be reached
		}

		/// <summary>
		/// Resolves the method/field
		/// </summary>
		/// <returns>A <see cref="MethodDef"/> or a <see cref="FieldDef"/> instance or <c>null</c>
		/// if it couldn't be resolved.</returns>
		public IMemberForwarded Resolve() {
			if (ownerModule == null)
				return null;
			return ownerModule.Context.Resolver.Resolve(this);
		}

		/// <summary>
		/// Resolves the method/field
		/// </summary>
		/// <returns>A <see cref="MethodDef"/> or a <see cref="FieldDef"/> instance</returns>
		/// <exception cref="MemberRefResolveException">If the method/field couldn't be resolved</exception>
		public IMemberForwarded ResolveThrow() {
			var memberDef = Resolve();
			if (memberDef != null)
				return memberDef;
			throw new MemberRefResolveException(string.Format("Could not resolve method/field: {0}", this));
		}

		/// <summary>
		/// Resolves the field
		/// </summary>
		/// <returns>A <see cref="FieldDef"/> instance or <c>null</c> if it couldn't be resolved.</returns>
		public FieldDef ResolveField() {
			return Resolve() as FieldDef;
		}

		/// <summary>
		/// Resolves the field
		/// </summary>
		/// <returns>A <see cref="FieldDef"/> instance</returns>
		/// <exception cref="MemberRefResolveException">If the field couldn't be resolved</exception>
		public FieldDef ResolveFieldThrow() {
			var field = ResolveField();
			if (field != null)
				return field;
			throw new MemberRefResolveException(string.Format("Could not resolve field: {0}", this));
		}

		/// <summary>
		/// Resolves the method
		/// </summary>
		/// <returns>A <see cref="MethodDef"/> instance or <c>null</c> if it couldn't be resolved.</returns>
		public MethodDef ResolveMethod() {
			return Resolve() as MethodDef;
		}

		/// <summary>
		/// Resolves the method
		/// </summary>
		/// <returns>A <see cref="MethodDef"/> instance</returns>
		/// <exception cref="MemberRefResolveException">If the method couldn't be resolved</exception>
		public MethodDef ResolveMethodThrow() {
			var method = ResolveMethod();
			if (method != null)
				return method;
			throw new MemberRefResolveException(string.Format("Could not resolve method: {0}", this));
		}

		/// <inheritdoc/>
		public override string ToString() {
			return FullName;
		}
	}

	/// <summary>
	/// A MemberRef row created by the user and not present in the original .NET file
	/// </summary>
	public class MemberRefUser : MemberRef {
		IMemberRefParent @class;
		UTF8String name;
		CallingConventionSig signature;
		CustomAttributeCollection customAttributeCollection = new CustomAttributeCollection();

		/// <inheritdoc/>
		public override IMemberRefParent Class {
			get { return @class; }
			set { @class = value; }
		}

		/// <inheritdoc/>
		public override UTF8String Name {
			get { return name; }
			set { name = value; }
		}

		/// <inheritdoc/>
		public override CallingConventionSig Signature {
			get { return signature; }
			set { signature = value; }
		}

		/// <inheritdoc/>
		public override CustomAttributeCollection CustomAttributes {
			get { return customAttributeCollection; }
		}

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="ownerModule">Owner module</param>
		public MemberRefUser(ModuleDef ownerModule) {
			this.ownerModule = ownerModule;
		}

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="ownerModule">Owner module</param>
		/// <param name="name">Name of ref</param>
		public MemberRefUser(ModuleDef ownerModule, UTF8String name) {
			this.ownerModule = ownerModule;
			this.name = name;
		}

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="ownerModule">Owner module</param>
		/// <param name="name">Name of field ref</param>
		/// <param name="sig">Field sig</param>
		public MemberRefUser(ModuleDef ownerModule, UTF8String name, FieldSig sig)
			: this(ownerModule, name, sig, null) {
		}

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="ownerModule">Owner module</param>
		/// <param name="name">Name of field ref</param>
		/// <param name="sig">Field sig</param>
		/// <param name="class">Owner of field</param>
		public MemberRefUser(ModuleDef ownerModule, UTF8String name, FieldSig sig, IMemberRefParent @class) {
			this.ownerModule = ownerModule;
			this.name = name;
			this.@class = @class;
			this.signature = sig;
		}

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="ownerModule">Owner module</param>
		/// <param name="name">Name of method ref</param>
		/// <param name="sig">Method sig</param>
		public MemberRefUser(ModuleDef ownerModule, UTF8String name, MethodSig sig)
			: this(ownerModule, name, sig, null) {
		}

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="ownerModule">Owner module</param>
		/// <param name="name">Name of method ref</param>
		/// <param name="sig">Method sig</param>
		/// <param name="class">Owner of method</param>
		public MemberRefUser(ModuleDef ownerModule, UTF8String name, MethodSig sig, IMemberRefParent @class) {
			this.ownerModule = ownerModule;
			this.name = name;
			this.@class = @class;
			this.signature = sig;
		}

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="ownerModule">Owner module</param>
		/// <param name="name">Name of ref</param>
		public MemberRefUser(ModuleDef ownerModule, string name)
			: this(ownerModule, new UTF8String(name)) {
		}

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="ownerModule">Owner module</param>
		/// <param name="name">Name of field ref</param>
		/// <param name="sig">Field sig</param>
		public MemberRefUser(ModuleDef ownerModule, string name, FieldSig sig)
			: this(ownerModule, name, sig, null) {
		}

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="ownerModule">Owner module</param>
		/// <param name="name">Name of field ref</param>
		/// <param name="sig">Field sig</param>
		/// <param name="class">Owner of field</param>
		public MemberRefUser(ModuleDef ownerModule, string name, FieldSig sig, IMemberRefParent @class)
			: this(ownerModule, new UTF8String(name), sig, @class) {
		}

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="ownerModule">Owner module</param>
		/// <param name="name">Name of method ref</param>
		/// <param name="sig">Method sig</param>
		public MemberRefUser(ModuleDef ownerModule, string name, MethodSig sig)
			: this(ownerModule, name, sig, null) {
		}

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="ownerModule">Owner module</param>
		/// <param name="name">Name of method ref</param>
		/// <param name="sig">Method sig</param>
		/// <param name="class">Owner of method</param>
		public MemberRefUser(ModuleDef ownerModule, string name, MethodSig sig, IMemberRefParent @class)
			: this(ownerModule, new UTF8String(name), sig, @class) {
		}
	}

	/// <summary>
	/// Created from a row in the MemberRef table
	/// </summary>
	sealed class MemberRefMD : MemberRef {
		/// <summary>The module where this instance is located</summary>
		ModuleDefMD readerModule;
		/// <summary>The raw table row. It's <c>null</c> until <see cref="InitializeRawRow"/> is called</summary>
		RawMemberRefRow rawRow;

		UserValue<IMemberRefParent> @class;
		UserValue<UTF8String> name;
		UserValue<CallingConventionSig> signature;
		CustomAttributeCollection customAttributeCollection;

		/// <inheritdoc/>
		public override IMemberRefParent Class {
			get { return @class.Value; }
			set { @class.Value = value; }
		}

		/// <inheritdoc/>
		public override UTF8String Name {
			get { return name.Value; }
			set { name.Value = value; }
		}

		/// <inheritdoc/>
		public override CallingConventionSig Signature {
			get { return signature.Value; }
			set { signature.Value = value; }
		}

		/// <inheritdoc/>
		public override CustomAttributeCollection CustomAttributes {
			get {
				if (customAttributeCollection == null) {
					var list = readerModule.MetaData.GetCustomAttributeRidList(Table.MemberRef, rid);
					customAttributeCollection = new CustomAttributeCollection((int)list.Length, list, (list2, index) => readerModule.ReadCustomAttribute(((RidList)list2)[index]));
				}
				return customAttributeCollection;
			}
		}

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="readerModule">The module which contains this <c>MemberRef</c> row</param>
		/// <param name="rid">Row ID</param>
		/// <exception cref="ArgumentNullException">If <paramref name="readerModule"/> is <c>null</c></exception>
		/// <exception cref="ArgumentException">If <paramref name="rid"/> is invalid</exception>
		public MemberRefMD(ModuleDefMD readerModule, uint rid) {
#if DEBUG
			if (readerModule == null)
				throw new ArgumentNullException("readerModule");
			if (readerModule.TablesStream.Get(Table.MemberRef).IsInvalidRID(rid))
				throw new BadImageFormatException(string.Format("MemberRef rid {0} does not exist", rid));
#endif
			this.rid = rid;
			this.readerModule = readerModule;
			this.ownerModule = readerModule;
			Initialize();
		}

		void Initialize() {
			@class.ReadOriginalValue = () => {
				InitializeRawRow();
				return readerModule.ResolveMemberRefParent(rawRow.Class);
			};
			name.ReadOriginalValue = () => {
				InitializeRawRow();
				return readerModule.StringsStream.ReadNoNull(rawRow.Name);
			};
			signature.ReadOriginalValue = () => {
				InitializeRawRow();
				return readerModule.ReadSignature(rawRow.Signature);
			};
		}

		void InitializeRawRow() {
			if (rawRow != null)
				return;
			rawRow = readerModule.TablesStream.ReadMemberRefRow(rid);
		}
	}
}
