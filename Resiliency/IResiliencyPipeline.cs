namespace Resiliency
{
    public interface IResiliencyPipeline
    {
        Task Execute(Func<Task> action);
        Task<T?> Execute<T>(Func<Task<T>> action, T? defaultValue = default);

    }
}
