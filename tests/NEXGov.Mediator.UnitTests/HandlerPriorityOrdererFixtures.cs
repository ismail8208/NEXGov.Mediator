// MED-015 fixtures for direct HandlerPriorityOrderer tests. Deliberately
// spread across several nested namespaces (mirroring a realistic
// feature-folder layout) so namespace-proximity comparisons have real,
// distinct depths to compare — a flat single-namespace fixture set
// couldn't exercise parent/grandparent/unrelated relationships at all.

namespace NEXGov.Mediator.UnitTests.OrdererFixtures.Feature.Commands
{
    // Stands in for "the request" in these tests — HandlerPriorityOrderer
    // only inspects Type metadata, so a request stand-in never needs to
    // actually implement IRequest.
    internal sealed class RequestInFeatureCommands;

    internal sealed class ExactNamespaceCandidate;

    internal sealed class ExactNamespaceCandidateSecond;
}

namespace NEXGov.Mediator.UnitTests.OrdererFixtures.Feature
{
    internal sealed class ParentNamespaceCandidate;
}

namespace NEXGov.Mediator.UnitTests.OrdererFixtures
{
    internal sealed class GrandparentNamespaceCandidate;
}

namespace NEXGov.Mediator.UnitTests.OrdererFixtures.Other
{
    internal sealed class UnrelatedNamespaceCandidate;
}

namespace NEXGov.Mediator.UnitTests
{
    internal class BaseOrdererCandidate;

    internal sealed class DerivedOrdererCandidate : BaseOrdererCandidate;

    internal abstract class GrandBaseOrdererCandidate;

    internal class MiddleOrdererCandidate : GrandBaseOrdererCandidate;

    internal sealed class LeafOrdererCandidate : MiddleOrdererCandidate;
}
