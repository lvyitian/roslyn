﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Test.Utilities;
using Microsoft.CodeAnalysis.Test.Utilities;
using Roslyn.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.CSharp.UnitTests
{
    public class RecordTests : CompilingTestBase
    {
        private static CSharpCompilation CreateCompilation(CSharpTestSource source)
            => CSharpTestBase.CreateCompilation(new[] { source, IsExternalInitTypeDefinition }, parseOptions: TestOptions.RegularPreview);

        private CompilationVerifier CompileAndVerify(CSharpTestSource src, string? expectedOutput = null)
            => base.CompileAndVerify(new[] { src, IsExternalInitTypeDefinition },
                expectedOutput: expectedOutput,
                parseOptions: TestOptions.RegularPreview,
                // init-only fails verification
                verify: Verification.Skipped);

        [Fact]
        public void GeneratedConstructor()
        {
            var comp = CreateCompilation(@"data class C(int x, string y);");
            comp.VerifyDiagnostics();
            var c = comp.GlobalNamespace.GetTypeMember("C");
            var ctor = (MethodSymbol)c.GetMembers(".ctor")[0];
            Assert.Equal(2, ctor.ParameterCount);

            var x = ctor.Parameters[0];
            Assert.Equal(SpecialType.System_Int32, x.Type.SpecialType);
            Assert.Equal("x", x.Name);

            var y = ctor.Parameters[1];
            Assert.Equal(SpecialType.System_String, y.Type.SpecialType);
            Assert.Equal("y", y.Name);
        }

        [Fact]
        public void GeneratedConstructorDefaultValues()
        {
            var comp = CreateCompilation(@"data class C<T>(int x, T t = default);");
            comp.VerifyDiagnostics();
            var c = comp.GlobalNamespace.GetTypeMember("C");
            Assert.Equal(1, c.Arity);
            var ctor = (MethodSymbol)c.GetMembers(".ctor")[0];
            Assert.Equal(0, ctor.Arity);
            Assert.Equal(2, ctor.ParameterCount);

            var x = ctor.Parameters[0];
            Assert.Equal(SpecialType.System_Int32, x.Type.SpecialType);
            Assert.Equal("x", x.Name);

            var t = ctor.Parameters[1];
            Assert.Equal(c.TypeParameters[0], t.Type);
            Assert.Equal("t", t.Name);
        }

        [Fact]
        public void RecordExistingConstructor1()
        {
            var comp = CreateCompilation(@"
data class C(int x, string y)
{
    public C(int a, string b)
    {
    }
}");
            comp.VerifyDiagnostics(
                // (2,13): error CS8762: There cannot be a primary constructor and a member constructor with the same parameter types.
                // data class C(int x, string y)
                Diagnostic(ErrorCode.ERR_DuplicateRecordConstructor, "(int x, string y)").WithLocation(2, 13)
            );
            var c = comp.GlobalNamespace.GetTypeMember("C");
            var ctor = (MethodSymbol)c.GetMembers(".ctor")[0];
            Assert.Equal(2, ctor.ParameterCount);

            var a = ctor.Parameters[0];
            Assert.Equal(SpecialType.System_Int32, a.Type.SpecialType);
            Assert.Equal("a", a.Name);

            var b = ctor.Parameters[1];
            Assert.Equal(SpecialType.System_String, b.Type.SpecialType);
            Assert.Equal("b", b.Name);
        }

        [Fact]
        public void RecordExistingConstructor01()
        {
            var comp = CreateCompilation(@"
data class C(int x, string y)
{
    public C(int a, int b) // overload
    {
    }
}");
            comp.VerifyDiagnostics();
            var c = comp.GlobalNamespace.GetTypeMember("C");
            var ctors = c.GetMembers(".ctor");
            Assert.Equal(3, ctors.Length);

            foreach (MethodSymbol ctor in ctors)
            {
                if (ctor.ParameterCount == 2)
                {
                    var p1 = ctor.Parameters[0];
                    Assert.Equal(SpecialType.System_Int32, p1.Type.SpecialType);
                    var p2 = ctor.Parameters[1];
                    if (ctor is SynthesizedRecordConstructor)
                    {
                        Assert.Equal("x", p1.Name);
                        Assert.Equal("y", p2.Name);
                        Assert.Equal(SpecialType.System_String, p2.Type.SpecialType);
                    }
                    else
                    {
                        Assert.Equal("a", p1.Name);
                        Assert.Equal("b", p2.Name);
                        Assert.Equal(SpecialType.System_Int32, p2.Type.SpecialType);
                    }
                }
                else
                {
                    Assert.Equal(1, ctor.ParameterCount);
                    Assert.True(c.Equals(ctor.Parameters[0].Type, TypeCompareKind.ConsiderEverything));
                }
            }
        }

        [Fact]
        public void GeneratedProperties()
        {
            var comp = CreateCompilation("data class C(int x, int y);");
            comp.VerifyDiagnostics();
            var c = comp.GlobalNamespace.GetTypeMember("C");

            var x = (SourceOrRecordPropertySymbol)c.GetProperty("x");
            Assert.NotNull(x.GetMethod);
            Assert.Equal(MethodKind.PropertyGet, x.GetMethod.MethodKind);
            Assert.Equal(SpecialType.System_Int32, x.Type.SpecialType);
            Assert.False(x.IsReadOnly);
            Assert.False(x.IsWriteOnly);
            Assert.Equal(Accessibility.Public, x.DeclaredAccessibility);
            Assert.False(x.IsVirtual);
            Assert.False(x.IsStatic);
            Assert.Equal(c, x.ContainingType);
            Assert.Equal(c, x.ContainingSymbol);

            var backing = x.BackingField;
            Assert.Equal(x, backing.AssociatedSymbol);
            Assert.Equal(c, backing.ContainingSymbol);
            Assert.Equal(c, backing.ContainingType);

            var getAccessor = x.GetMethod;
            Assert.Equal(x, getAccessor.AssociatedSymbol);
            Assert.Equal(c, getAccessor.ContainingSymbol);
            Assert.Equal(c, getAccessor.ContainingType);
            Assert.Equal(Accessibility.Public, getAccessor.DeclaredAccessibility);

            var setAccessor = x.SetMethod;
            Assert.Equal(x, setAccessor.AssociatedSymbol);
            Assert.Equal(c, setAccessor.ContainingSymbol);
            Assert.Equal(c, setAccessor.ContainingType);
            Assert.Equal(Accessibility.Public, setAccessor.DeclaredAccessibility);
            Assert.True(setAccessor.IsInitOnly);

            var y = (SourceOrRecordPropertySymbol)c.GetProperty("y");
            Assert.NotNull(y.GetMethod);
            Assert.Equal(MethodKind.PropertyGet, y.GetMethod.MethodKind);
            Assert.Equal(SpecialType.System_Int32, y.Type.SpecialType);
            Assert.False(y.IsReadOnly);
            Assert.False(y.IsWriteOnly);
            Assert.Equal(Accessibility.Public, y.DeclaredAccessibility);
            Assert.False(x.IsVirtual);
            Assert.False(x.IsStatic);
            Assert.Equal(c, y.ContainingType);
            Assert.Equal(c, y.ContainingSymbol);

            backing = y.BackingField;
            Assert.Equal(y, backing.AssociatedSymbol);
            Assert.Equal(c, backing.ContainingSymbol);
            Assert.Equal(c, backing.ContainingType);

            getAccessor = y.GetMethod;
            Assert.Equal(y, getAccessor.AssociatedSymbol);
            Assert.Equal(c, getAccessor.ContainingSymbol);
            Assert.Equal(c, getAccessor.ContainingType);

            setAccessor = y.SetMethod;
            Assert.Equal(y, setAccessor.AssociatedSymbol);
            Assert.Equal(c, setAccessor.ContainingSymbol);
            Assert.Equal(c, setAccessor.ContainingType);
            Assert.Equal(Accessibility.Public, setAccessor.DeclaredAccessibility);
            Assert.True(setAccessor.IsInitOnly);
        }

        [Fact]
        public void RecordClone1()
        {
            var comp = CreateCompilation("data class C(int x, int y);");
            comp.VerifyDiagnostics();

            var c = comp.GlobalNamespace.GetTypeMember("C");
            var clone = c.GetMethod(WellKnownMemberNames.CloneMethodName);
            Assert.Equal(0, clone.Arity);
            Assert.Equal(0, clone.ParameterCount);
            Assert.Equal(c, clone.ReturnType);

            var ctor = (MethodSymbol)c.GetMembers(".ctor")[1];
            Assert.Equal(1, ctor.ParameterCount);
            Assert.True(ctor.Parameters[0].Type.Equals(c, TypeCompareKind.ConsiderEverything));

            var verifier = CompileAndVerify(comp, verify: Verification.Fails);
            verifier.VerifyIL("C." + WellKnownMemberNames.CloneMethodName, @"
{
  // Code size        7 (0x7)
  .maxstack  1
  IL_0000:  ldarg.0
  IL_0001:  newobj     ""C..ctor(C)""
  IL_0006:  ret
}
");
            verifier.VerifyIL("C..ctor(C)", @"
{
  // Code size       31 (0x1f)
  .maxstack  2
  IL_0000:  ldarg.0
  IL_0001:  call       ""object..ctor()""
  IL_0006:  ldarg.0
  IL_0007:  ldarg.1
  IL_0008:  ldfld      ""int C.<x>k__BackingField""
  IL_000d:  stfld      ""int C.<x>k__BackingField""
  IL_0012:  ldarg.0
  IL_0013:  ldarg.1
  IL_0014:  ldfld      ""int C.<y>k__BackingField""
  IL_0019:  stfld      ""int C.<y>k__BackingField""
  IL_001e:  ret
}");
        }

        [Fact]
        public void RecordClone2()
        {
            var comp = CreateCompilation(@"
data class C(int x, int y)
{
    public C(C other) { }
}");
            comp.VerifyDiagnostics();

            var c = comp.GlobalNamespace.GetTypeMember("C");
            var clone = c.GetMethod(WellKnownMemberNames.CloneMethodName);
            Assert.Equal(0, clone.Arity);
            Assert.Equal(0, clone.ParameterCount);
            Assert.Equal(c, clone.ReturnType);

            var ctor = (MethodSymbol)c.GetMembers(".ctor")[0];
            Assert.Equal(1, ctor.ParameterCount);
            Assert.True(ctor.Parameters[0].Type.Equals(c, TypeCompareKind.ConsiderEverything));

            var verifier = CompileAndVerify(comp, verify: Verification.Fails);
            verifier.VerifyIL("C." + WellKnownMemberNames.CloneMethodName, @"
{
  // Code size        7 (0x7)
  .maxstack  1
  IL_0000:  ldarg.0
  IL_0001:  newobj     ""C..ctor(C)""
  IL_0006:  ret
}
");
            verifier.VerifyIL("C..ctor(C)", @"
{
  // Code size        7 (0x7)
  .maxstack  1
  IL_0000:  ldarg.0
  IL_0001:  call       ""object..ctor()""
  IL_0006:  ret
}");
        }

        [Fact]
        void NominalRecordEquals()
        {
            var verifier = CompileAndVerify(@"
using System;
data class C
{
    private int X;
    private int Y { get; set; }
    private event Action E;

    public static void Main()
    {
        var c = new C { X = 1, Y = 2 };
        c.E = () => { };
        var c2 = new C { X = 1, Y = 2 };
        c2.E = () => { };
        Console.WriteLine(c.Equals(c2));
        Console.WriteLine(c.Equals((object)c2));
        c2.E = c.E;
        Console.WriteLine(c.Equals(c2));
        Console.WriteLine(c.Equals((object)c2));
    }
}", expectedOutput: @"False
False
True
True");
            verifier.VerifyIL("C.Equals(object)", @"
{
  // Code size       13 (0xd)
  .maxstack  2
  IL_0000:  ldarg.0
  IL_0001:  ldarg.1
  IL_0002:  isinst     ""C""
  IL_0007:  call       ""bool C.Equals(C)""
  IL_000c:  ret
}");
            verifier.VerifyIL("C.Equals(C)", @"
{
  // Code size       76 (0x4c)
  .maxstack  3
  IL_0000:  ldarg.1
  IL_0001:  brfalse.s  IL_004a
  IL_0003:  call       ""System.Collections.Generic.EqualityComparer<int> System.Collections.Generic.EqualityComparer<int>.Default.get""
  IL_0008:  ldarg.0
  IL_0009:  ldfld      ""int C.X""
  IL_000e:  ldarg.1
  IL_000f:  ldfld      ""int C.X""
  IL_0014:  callvirt   ""bool System.Collections.Generic.EqualityComparer<int>.Equals(int, int)""
  IL_0019:  brfalse.s  IL_004a
  IL_001b:  call       ""System.Collections.Generic.EqualityComparer<int> System.Collections.Generic.EqualityComparer<int>.Default.get""
  IL_0020:  ldarg.0
  IL_0021:  ldfld      ""int C.<Y>k__BackingField""
  IL_0026:  ldarg.1
  IL_0027:  ldfld      ""int C.<Y>k__BackingField""
  IL_002c:  callvirt   ""bool System.Collections.Generic.EqualityComparer<int>.Equals(int, int)""
  IL_0031:  brfalse.s  IL_004a
  IL_0033:  call       ""System.Collections.Generic.EqualityComparer<System.Action> System.Collections.Generic.EqualityComparer<System.Action>.Default.get""
  IL_0038:  ldarg.0
  IL_0039:  ldfld      ""System.Action C.E""
  IL_003e:  ldarg.1
  IL_003f:  ldfld      ""System.Action C.E""
  IL_0044:  callvirt   ""bool System.Collections.Generic.EqualityComparer<System.Action>.Equals(System.Action, System.Action)""
  IL_0049:  ret
  IL_004a:  ldc.i4.0
  IL_004b:  ret
}");
        }

        [Fact]
        public void PositionalAndNominalSameEquals()
        {
            var v1 = CompileAndVerify(@"
using System;
data class C(int X, string Y)
{
    public event Action E;
}
");
            var v2 = CompileAndVerify(@"
using System;
data class C
{
    public int X { get; }
    public string Y { get; }
    public event Action E;
}");
            Assert.Equal(v1.VisualizeIL("C.Equals(C)"), v2.VisualizeIL("C.Equals(C)"));
            Assert.Equal(v1.VisualizeIL("C.Equals(object)"), v2.VisualizeIL("C.Equals(object)"));
        }

        [Fact]
        public void NominalRecordMembers()
        {
            var comp = CreateCompilation(@"
#nullable enable
data class C
{
    public int X { get; init; }
    public string Y { get; init; }
}");
            var members = comp.GlobalNamespace.GetTypeMember("C").GetMembers();
            AssertEx.Equal(new[] {
                "System.Int32 C.<X>k__BackingField",
                "System.Int32 C.X { get; init; }",
                "System.Int32 C.X.get",
                "void C.X.init",
                "System.String! C.<Y>k__BackingField",
                "System.String! C.Y { get; init; }",
                "System.String! C.Y.get",
                "void C.Y.init",
                "C! C.Clone()",
                "System.Boolean C.Equals(C? )",
                "System.Boolean C.Equals(System.Object? )",
                "System.Int32 C.GetHashCode()",
                "C.C(C! )",
                "C.C()",
            }, members.Select(m => m.ToTestDisplayString(includeNonNullable: true)));
        }
    }
}