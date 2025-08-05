using Polly;

namespace Resiliency
{
    public interface IPolicyBuilder
    {
        IAsyncPolicy Build();

    }
}
