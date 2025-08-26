
namespace MedConnect.Services
{
    public interface ICircuitBreakerService
    {
        bool IsOpen { get; }
        void RecordFailure();
        void Reset();
        bool CanExecute();
        Task ExecuteAsync(Func<Task> value);
    }
}