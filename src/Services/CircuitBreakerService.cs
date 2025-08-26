using Polly;
using Polly.CircuitBreaker;

namespace MedConnect.Services
{
    public class CircuitBreakerService : ICircuitBreakerService
    {
        private readonly AsyncCircuitBreakerPolicy _circuitBreakerPolicy;

        public CircuitBreakerService(int exceptionsAllowedBeforeBreaking=3)
        {
            _circuitBreakerPolicy = Policy
                .Handle<Exception>()
                .CircuitBreakerAsync(3, TimeSpan.FromSeconds(30));
        }

        public async Task ExecuteAsync(Func<Task> action)
        {
            await _circuitBreakerPolicy.ExecuteAsync(action);
        }

        public bool IsOpen => _circuitBreakerPolicy.CircuitState == CircuitState.Open;

        public void RecordFailure()
        {
            // Polly does not expose direct failure recording, so this is a placeholder.
            // You may want to trigger a failure by executing a failing action.
        }

        public void Reset()
        {
            _circuitBreakerPolicy.Reset();
        }

        public bool CanExecute()
        {
            return _circuitBreakerPolicy.CircuitState != CircuitState.Open;
        }
    }
}