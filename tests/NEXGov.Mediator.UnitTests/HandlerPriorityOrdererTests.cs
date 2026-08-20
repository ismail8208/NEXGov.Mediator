using NEXGov.Mediator.Internal;
using NEXGov.Mediator.UnitTests.OrdererFixtures;
using NEXGov.Mediator.UnitTests.OrdererFixtures.Feature;
using NEXGov.Mediator.UnitTests.OrdererFixtures.Feature.Commands;
using NEXGov.Mediator.UnitTests.OrdererFixtures.Other;

namespace NEXGov.Mediator.UnitTests;

// MED-015: direct tests of the internal HandlerPriorityOrderer, exercised
// via InternalsVisibleTo rather than only indirectly through Send —
// asserting the observable ordered sequence of candidate objects it
// returns for a given request type and candidate set.
public class HandlerPriorityOrdererTests
{
    private static Type RequestType => typeof(RequestInFeatureCommands);

    [Fact]
    public void Prioritize_ZeroCandidates_ReturnsEmpty()
    {
        var result = HandlerPriorityOrderer.Prioritize([], RequestType);

        Assert.Empty(result);
    }

    [Fact]
    public void Prioritize_OneCandidate_ReturnsItUnchanged()
    {
        var only = new ExactNamespaceCandidate();

        var result = HandlerPriorityOrderer.Prioritize([only], RequestType);

        Assert.Same(only, Assert.Single(result));
    }

    // --- Base/derived handler implementation priority (item 3) ---

    [Fact]
    public void Prioritize_BaseOnly_Survives()
    {
        var baseInstance = new BaseOrdererCandidate();
        var other = new ExactNamespaceCandidate();

        var result = HandlerPriorityOrderer.Prioritize([baseInstance, other], RequestType);

        Assert.Contains(baseInstance, result);
    }

    [Fact]
    public void Prioritize_DerivedOnly_Survives()
    {
        var derived = new DerivedOrdererCandidate();
        var other = new ExactNamespaceCandidate();

        var result = HandlerPriorityOrderer.Prioritize([derived, other], RequestType);

        Assert.Contains(derived, result);
    }

    [Fact]
    public void Prioritize_BaseAndDerivedBothRegistered_OnlyDerivedSurvives()
    {
        var baseInstance = new BaseOrdererCandidate();
        var derived = new DerivedOrdererCandidate();

        var result = HandlerPriorityOrderer.Prioritize([baseInstance, derived], RequestType);

        Assert.Equal([derived], result);
    }

    [Fact]
    public void Prioritize_MultipleInheritanceLevels_OnlyTheMostDerivedSurvives()
    {
        var middle = new MiddleOrdererCandidate();
        var leaf = new LeafOrdererCandidate();

        var result = HandlerPriorityOrderer.Prioritize([middle, leaf], RequestType);

        Assert.Equal([leaf], result);
    }

    [Fact]
    public void Prioritize_ExactSameConcreteTypeRegisteredTwice_CollapsesToOne()
    {
        // Verified against current source: a type is trivially assignable
        // from itself, so RemoveOverridden collapses literal duplicate
        // registrations of the identical concrete type down to one
        // surviving instance (the later-registered one, per the pairwise
        // i<j comparison order).
        var first = new ExactNamespaceCandidate();
        var second = new ExactNamespaceCandidate();

        var result = HandlerPriorityOrderer.Prioritize([first, second], RequestType);

        Assert.Equal([second], result);
    }

    // --- Namespace proximity (item 5) ---

    [Fact]
    public void Prioritize_ExactNamespaceMatch_OutranksParentNamespace()
    {
        var parent = new ParentNamespaceCandidate();
        var exact = new ExactNamespaceCandidate();

        var result = HandlerPriorityOrderer.Prioritize([parent, exact], RequestType);

        Assert.Equal([exact, parent], result);
    }

    [Fact]
    public void Prioritize_ParentNamespace_OutranksGrandparentNamespace()
    {
        var grandparent = new GrandparentNamespaceCandidate();
        var parent = new ParentNamespaceCandidate();

        var result = HandlerPriorityOrderer.Prioritize([grandparent, parent], RequestType);

        Assert.Equal([parent, grandparent], result);
    }

    [Fact]
    public void Prioritize_ParentNamespace_OutranksUnrelatedNamespace()
    {
        var unrelated = new UnrelatedNamespaceCandidate();
        var parent = new ParentNamespaceCandidate();

        var result = HandlerPriorityOrderer.Prioritize([unrelated, parent], RequestType);

        Assert.Equal([parent, unrelated], result);
    }

    [Fact]
    public void Prioritize_FullDepthOrdering_ExactThenParentThenGrandparentThenUnrelated()
    {
        var unrelated = new UnrelatedNamespaceCandidate();
        var grandparent = new GrandparentNamespaceCandidate();
        var parent = new ParentNamespaceCandidate();
        var exact = new ExactNamespaceCandidate();

        // Registered in deliberately reversed priority order.
        var result = HandlerPriorityOrderer.Prioritize([unrelated, grandparent, parent, exact], RequestType);

        Assert.Equal([exact, parent, grandparent, unrelated], result);
    }

    // --- Assembly proximity (item 4) ---

    [Fact]
    public void Prioritize_SameAssemblyCandidate_OutranksForeignAssemblyCandidate()
    {
        // System.Text.StringBuilder is deliberately used as the "foreign
        // assembly" candidate: it lives in a completely different assembly
        // and namespace than the request, with no inheritance relation to
        // any other fixture (unlike `object`, which is trivially a base
        // type of everything and would instead trigger RemoveOverridden).
        var foreignAssembly = new System.Text.StringBuilder();
        var sameAssembly = new UnrelatedNamespaceCandidate(); // still in this test assembly

        var result = HandlerPriorityOrderer.Prioritize([foreignAssembly, sameAssembly], RequestType);

        Assert.Equal([sameAssembly, foreignAssembly], result);
    }

    // --- Tie behavior (item 6) ---

    [Fact]
    public void Prioritize_EquivalentPriority_PreservesOriginalOrder()
    {
        // Two candidates in the exact same namespace as each other (and
        // as the request) have identical computed priority — deliberate,
        // documented tie-break: original (provider) order is preserved.
        var a = new ExactNamespaceCandidate();
        var b = new ExactNamespaceCandidateSecond();

        var result = HandlerPriorityOrderer.Prioritize([a, b], RequestType);

        Assert.Equal([a, b], result);

        var reversedResult = HandlerPriorityOrderer.Prioritize([b, a], RequestType);

        Assert.Equal([b, a], reversedResult);
    }
}
