using System.Reflection;

namespace NEXGov.Mediator.CompatibilityTests;

// Verifies the public API shape of the MED-014 Unit type matches the
// compatibility surface documented in docs/COMPATIBILITY.md, verified
// against current MediatR source (MediatR.Contracts/Unit.cs) rather than
// assumed from an older version's shape.
public class UnitCompatibilityTests
{
    private const BindingFlags DeclaredPublicStatic = BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly;
    private const BindingFlags DeclaredPublicInstance = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    [Fact]
    public void Unit_HasExpectedFullNameAndIsPublicReadOnlyStruct()
    {
        var type = typeof(Unit);

        Assert.Equal("NEXGov.Mediator.Unit", type.FullName);
        Assert.True(type.IsValueType);
        Assert.True(type.IsPublic);
        // A readonly struct is marked with IsReadOnlyAttribute at the type level.
        Assert.Contains(type.GetCustomAttributes(inherit: false), a => a.GetType().Name == "IsReadOnlyAttribute");
    }

    [Fact]
    public void Unit_ImplementsExpectedInterfaces()
    {
        var interfaces = typeof(Unit).GetInterfaces();

        Assert.Contains(typeof(IEquatable<Unit>), interfaces);
        Assert.Contains(typeof(IComparable<Unit>), interfaces);
        Assert.Contains(typeof(IComparable), interfaces);
        Assert.Equal(3, interfaces.Length);
    }

    [Fact]
    public void Unit_Value_IsPublicStaticGetOnlyProperty_ReturningByRef()
    {
        // Verified current source: "public static ref readonly Unit Value => ref _value;" —
        // asserted here at the level reflection can observe robustly (by-ref
        // return of Unit), without overfitting the compiler-emitted
        // readonly-ref modifier, which is an implementation detail beyond
        // what a consumer observes when simply reading Unit.Value.
        var property = typeof(Unit).GetProperty(nameof(Unit.Value), DeclaredPublicStatic);

        Assert.NotNull(property);
        Assert.True(property.CanRead);
        Assert.False(property.CanWrite);
        Assert.True(property.PropertyType.IsByRef);
        Assert.Equal(typeof(Unit), property.PropertyType.GetElementType());
    }

    [Fact]
    public void Unit_Task_IsPublicStaticGetOnlyProperty_OfTaskOfUnit()
    {
        var property = typeof(Unit).GetProperty(nameof(Unit.Task), DeclaredPublicStatic);

        Assert.NotNull(property);
        Assert.Equal(typeof(Task<Unit>), property.PropertyType);
        Assert.True(property.CanRead);
        Assert.False(property.CanWrite);
    }

    [Fact]
    public async Task Unit_Task_IsCompletedAndResultIsValue()
    {
        Assert.True(Unit.Task.IsCompletedSuccessfully);
        Assert.Equal(Unit.Value, await Unit.Task);
    }

    [Fact]
    public void Unit_Task_RepeatedAccess_ReturnsTheSameInstance()
    {
        Assert.Same(Unit.Task, Unit.Task);
    }

    [Fact]
    public void Unit_CompareTo_Unit_AlwaysReturnsZero()
    {
        Assert.Equal(0, Unit.Value.CompareTo(Unit.Value));
        Assert.Equal(0, new Unit().CompareTo(Unit.Value));
    }

    [Fact]
    public void Unit_CompareTo_ObjectViaIComparable_AlwaysReturnsZero()
    {
        IComparable comparable = Unit.Value;

        Assert.Equal(0, comparable.CompareTo(Unit.Value));
        Assert.Equal(0, comparable.CompareTo(null));
    }

    [Fact]
    public void Unit_Equals_Unit_AlwaysReturnsTrue()
    {
        Assert.True(Unit.Value.Equals(Unit.Value));
        Assert.True(new Unit().Equals(Unit.Value));
    }

    [Fact]
    public void Unit_Equals_Object_TrueForAnyUnit_FalseOtherwise()
    {
        Assert.True(Unit.Value.Equals((object)default(Unit)));
        Assert.False(Unit.Value.Equals("not a unit"));
        Assert.False(Unit.Value.Equals(null));
    }

    [Fact]
    public void Unit_EqualityOperators_AlwaysTrueAndFalseRespectively()
    {
#pragma warning disable CS1718 // Comparison made to same variable is intentional here.
        Assert.True(Unit.Value == Unit.Value);
#pragma warning restore CS1718
        Assert.False(Unit.Value != Unit.Value);
        Assert.True(Unit.Value == default(Unit));
    }

    [Fact]
    public void Unit_GetHashCode_IsZero()
    {
        Assert.Equal(0, Unit.Value.GetHashCode());
    }

    [Fact]
    public void Unit_ToString_ReturnsParentheses()
    {
        Assert.Equal("()", Unit.Value.ToString());
    }

    [Fact]
    public void Unit_HasNoPublicConstructors_OtherThanTheImplicitParameterlessOne()
    {
        Assert.Empty(typeof(Unit).GetConstructors());
    }

    [Fact]
    public void Unit_DeclaresExactlyTheExpectedPublicMembers()
    {
        var instanceMethods = typeof(Unit).GetMethods(DeclaredPublicInstance)
            .Where(m => !m.IsSpecialName)
            .Select(m => m.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["CompareTo", "Equals", "Equals", "GetHashCode", "ToString"], instanceMethods);

        var staticProperties = typeof(Unit).GetProperties(DeclaredPublicStatic).Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        Assert.Equal(["Task", "Value"], staticProperties);

        Assert.Empty(typeof(Unit).GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));
        Assert.Empty(typeof(Unit).GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly));
    }
}
