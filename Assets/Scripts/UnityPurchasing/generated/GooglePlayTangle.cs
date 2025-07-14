// WARNING: Do not modify! Generated file.

namespace UnityEngine.Purchasing.Security {
    public class GooglePlayTangle
    {
        private static byte[] data = System.Convert.FromBase64String("HZgYh3YkCK2+5Gth0LZZb+bh0MCFjguXpXYdRGYD2FO3qBxMpK1HkALmDRQsJiumR/t+LgF3JzLxvL5mwUzu33VK+aA/3CO1i74ett5Oe+kdVsdQFnUhnKY1yoPFFNPrzsaps2UKYcvvV8+RJ68f9uxi6hHlpf5tcM51IT9aQxFt1XM+xSRcJf7G02FnOGNyy8SVLNkjp0u0a1xGP1/hcFvY1tnpW9jT21vY2NltjnTN7G3mfh2jLzcbDig+BAWBIuFuX7vxehLpW9j76dTf0PNfkV8u1NjY2NzZ2lfZFwNBT7MUkEOdghlDoC3rOsgCWwwcgXNj92faK7qAMBAhGv8VfjqZ1EOvgTR71UdXlR/o8Sp4pJQ0fZ0XyLb7FZwVxtva2NnY");
        private static int[] order = new int[] { 6,4,3,10,13,8,8,7,13,11,13,13,13,13,14 };
        private static int key = 217;

        public static readonly bool IsPopulated = true;

        public static byte[] Data() {
        	if (IsPopulated == false)
        		return null;
            return Obfuscator.DeObfuscate(data, order, key);
        }
    }
}
