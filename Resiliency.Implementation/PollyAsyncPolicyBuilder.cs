using Polly;

namespace Resiliency.Implementation
{
    public class PollyAsyncPolicyBuilder: IPolicyBuilder
    {
        private readonly IAsyncPolicy _policy;
        public PollyAsyncPolicyBuilder(IAsyncPolicy policy )    
        {
            _policy = policy;
        }

        public IAsyncPolicy Build()
        {
            return _policy;
        }
    }
}
