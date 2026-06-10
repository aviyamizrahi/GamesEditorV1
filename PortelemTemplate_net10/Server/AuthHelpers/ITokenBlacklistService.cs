namespace UsersManager.Server
{    
    //לא לגעת - אבטחת משתמשים
    public interface ITokenBlacklistService
    {
        void AddToBlacklist(string token);
        bool IsBlacklisted(string token);
        void RemoveExpiredTokens();
    }
}
