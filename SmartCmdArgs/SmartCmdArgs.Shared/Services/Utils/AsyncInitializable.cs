using System.Threading.Tasks;

namespace SmartCmdArgs.Services
{
    internal interface IAsyncInitializable
    {
        Task InitializeAsync();
    }
}
