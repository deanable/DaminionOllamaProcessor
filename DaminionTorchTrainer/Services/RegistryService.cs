using Microsoft.Win32;
using System;
using System.Security.Cryptography;
using System.Text;

namespace DaminionTorchTrainer.Services
{
    /// <summary>
    /// Service for persisting application settings to the Windows Registry
    /// </summary>
    public class RegistryService
    {
        private const string RegistryKey = @"SOFTWARE\DaminionTorchTrainer";
        private const string EncryptionKey = "DaminionTorchTrainer2024!";

        /// <summary>
        /// Saves a string value to the registry
        /// </summary>
        public static void SaveString(string name, string value)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RegistryKey);
                key?.SetValue(name, value);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RegistryService] Error saving string {name}: {ex.Message}");
            }
        }

        /// <summary>
        /// Loads a string value from the registry
        /// </summary>
        public static string? LoadString(string name)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryKey);
                return key?.GetValue(name) as string;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RegistryService] Error loading string {name}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Saves an encrypted string value to the registry (for passwords)
        /// </summary>
        public static void SaveEncryptedString(string name, string value)
        {
            try
            {
                var encryptedValue = EncryptString(value);
                SaveString(name, encryptedValue);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RegistryService] Error saving encrypted string {name}: {ex.Message}");
            }
        }

        /// <summary>
        /// Loads an encrypted string value from the registry (for passwords)
        /// </summary>
        public static string? LoadEncryptedString(string name)
        {
            try
            {
                var encryptedValue = LoadString(name);
                if (string.IsNullOrEmpty(encryptedValue)) return null;
                
                return DecryptString(encryptedValue);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RegistryService] Error loading encrypted string {name}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Saves an integer value to the registry
        /// </summary>
        public static void SaveInt(string name, int value)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RegistryKey);
                key?.SetValue(name, value);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RegistryService] Error saving int {name}: {ex.Message}");
            }
        }

        /// <summary>
        /// Loads an integer value from the registry
        /// </summary>
        public static int LoadInt(string name, int defaultValue = 0)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryKey);
                var value = key?.GetValue(name);
                return value is int intValue ? intValue : defaultValue;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RegistryService] Error loading int {name}: {ex.Message}");
                return defaultValue;
            }
        }

        /// <summary>
        /// Saves a long value to the registry
        /// </summary>
        public static void SaveLong(string name, long value)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RegistryKey);
                key?.SetValue(name, value);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RegistryService] Error saving long {name}: {ex.Message}");
            }
        }

        /// <summary>
        /// Loads a long value from the registry
        /// </summary>
        public static long LoadLong(string name, long defaultValue = 0)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryKey);
                var value = key?.GetValue(name);
                return value is long longValue ? longValue : defaultValue;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RegistryService] Error loading long {name}: {ex.Message}");
                return defaultValue;
            }
        }

        /// <summary>
        /// Saves a boolean value to the registry
        /// </summary>
        public static void SaveBool(string name, bool value)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RegistryKey);
                key?.SetValue(name, value ? 1 : 0);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RegistryService] Error saving bool {name}: {ex.Message}");
            }
        }

        /// <summary>
        /// Loads a boolean value from the registry
        /// </summary>
        public static bool LoadBool(string name, bool defaultValue = false)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryKey);
                var value = key?.GetValue(name);
                return value is int intValue ? intValue == 1 : defaultValue;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RegistryService] Error loading bool {name}: {ex.Message}");
                return defaultValue;
            }
        }

        /// <summary>
        /// Saves a double value to the registry
        /// </summary>
        public static void SaveDouble(string name, double value)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RegistryKey);
                key?.SetValue(name, value.ToString());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RegistryService] Error saving double {name}: {ex.Message}");
            }
        }

        /// <summary>
        /// Loads a double value from the registry
        /// </summary>
        public static double LoadDouble(string name, double defaultValue = 0.0)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryKey);
                var value = key?.GetValue(name) as string;
                return double.TryParse(value, out double result) ? result : defaultValue;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RegistryService] Error loading double {name}: {ex.Message}");
                return defaultValue;
            }
        }

        /// <summary>
        /// Saves an enum value to the registry
        /// </summary>
        public static void SaveEnum<T>(string name, T value) where T : Enum
        {
            try
            {
                SaveString(name, value.ToString());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RegistryService] Error saving enum {name}: {ex.Message}");
            }
        }

        /// <summary>
        /// Loads an enum value from the registry
        /// </summary>
        public static T LoadEnum<T>(string name, T defaultValue) where T : Enum
        {
            try
            {
                var value = LoadString(name);
                if (string.IsNullOrEmpty(value)) return defaultValue;
                
                return (T)Enum.Parse(typeof(T), value);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RegistryService] Error loading enum {name}: {ex.Message}");
                return defaultValue;
            }
        }

        /// <summary>
        /// Clears all settings from the registry
        /// </summary>
        public static void ClearAllSettings()
        {
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(RegistryKey, false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RegistryService] Error clearing settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Simple encryption for sensitive data
        /// </summary>
        private static string EncryptString(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return string.Empty;
            
            try
            {
                var key = Encoding.UTF8.GetBytes(EncryptionKey.PadRight(32).Substring(0, 32));
                var iv = Encoding.UTF8.GetBytes(EncryptionKey.PadRight(16).Substring(0, 16));
                
                using var aes = Aes.Create();
                aes.Key = key;
                aes.IV = iv;
                
                using var encryptor = aes.CreateEncryptor();
                var plainBytes = Encoding.UTF8.GetBytes(plainText);
                var encryptedBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
                
                return Convert.ToBase64String(encryptedBytes);
            }
            catch
            {
                return plainText; // Fallback to plain text if encryption fails
            }
        }

        /// <summary>
        /// Simple decryption for sensitive data
        /// </summary>
        private static string DecryptString(string encryptedText)
        {
            if (string.IsNullOrEmpty(encryptedText)) return string.Empty;
            
            try
            {
                var key = Encoding.UTF8.GetBytes(EncryptionKey.PadRight(32).Substring(0, 32));
                var iv = Encoding.UTF8.GetBytes(EncryptionKey.PadRight(16).Substring(0, 16));
                
                using var aes = Aes.Create();
                aes.Key = key;
                aes.IV = iv;
                
                using var decryptor = aes.CreateDecryptor();
                var encryptedBytes = Convert.FromBase64String(encryptedText);
                var decryptedBytes = decryptor.TransformFinalBlock(encryptedBytes, 0, encryptedBytes.Length);
                
                return Encoding.UTF8.GetString(decryptedBytes);
            }
            catch
            {
                return encryptedText; // Fallback to encrypted text if decryption fails
            }
        }
    }
}
