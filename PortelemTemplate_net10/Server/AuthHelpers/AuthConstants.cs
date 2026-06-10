namespace UsersManager.Server
{
    /// <summary>
    /// Constants for authentication operations
    /// </summary>
    public static class AuthConstants
    {
        public const string BearerPrefix = "Bearer ";
        
        // Auth result codes
        public const string NoUser = "no user";
        public const string UserExists = "exist";
        public const string ErrorSignup = "error signup";
        public const string ErrorInsertToDb = "error insert to DB";
        public const string TokenInvalid = "token invalid";
        
        // Claim types
        public const string ClaimTypeEmail = "email";
        public const string ClaimTypeSub = "sub";
        public const string ClaimTypeGivenName = "given_name";
        public const string ClaimTypeFamilyName = "family_name";
    }
}

