using System.Reflection;

namespace NEXGov.Mediator.CompatibilityTests;

// Verifies the public API shape of the MED-006 IMediator contract matches
// the compatibility surface documented in docs/COMPATIBILITY.md.
public class MediatorInterfaceCompatibilityTests
{
    [Fact]
    public void IMediator_HasExpectedFullNameAndIsInterface()
    {
        var type = typeof(IMediator);

        Assert.Equal("NEXGov.Mediator.IMediator", type.FullName);
        Assert.True(type.IsInterface);
    }

    [Fact]
    public void IMediator_InheritsExactlyISenderAndIPublisher()
    {
        var baseInterfaces = typeof(IMediator).GetInterfaces();

        Assert.Equal(2, baseInterfaces.Length);
        Assert.Contains(typeof(ISender), baseInterfaces);
        Assert.Contains(typeof(IPublisher), baseInterfaces);
    }

    [Fact]
    public void IMediator_DeclaresNoOwnMembers()
    {
        var type = typeof(IMediator);
        const BindingFlags declaredInstance = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

        Assert.Empty(type.GetMethods(declaredInstance));
        Assert.Empty(type.GetProperties(declaredInstance));
        Assert.Empty(type.GetEvents(declaredInstance));
    }
}
