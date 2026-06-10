using UsersManager.Shared;

namespace UsersManager.Client
{
    public interface IAuthenticationService
    {

        event Action OnAuthenticationStateChanged;
        Task<User> GetUserFromClaimAsync();
        Task Logout();
        Task<bool> LoginWithPortelem();

    }
}
