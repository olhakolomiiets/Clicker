using shortid;

namespace CBS
{
    public static class NicknameHelper
    {
        public static string GenerateRandomName(string prefix)
        {
            return prefix+ShortId.Generate();
        }
    }
}