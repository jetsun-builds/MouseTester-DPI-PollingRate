using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace MouseTester
{
    public static class UsbDeviceNames
    {
        private static readonly Lazy<Dictionary<string, string>> Products =
            new Lazy<Dictionary<string, string>>(LoadProducts);
        private static readonly Regex Ids = new Regex(
            @"VID_([0-9A-F]{4})(?![0-9A-F]).*?PID_([0-9A-F]{4})(?![0-9A-F])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public static string Resolve(string path, string productName, string systemName)
        {
            string name = Clean(productName);
            if (!IsGeneric(name)) return name;
            name = Clean(systemName);
            if (!IsGeneric(name)) return name;
            Match match = Ids.Match(path ?? "");
            if (!match.Success) return "HID 鼠标";
            string key = match.Groups[1].Value.ToUpperInvariant() + ":" +
                match.Groups[2].Value.ToUpperInvariant();
            if (Products.Value.TryGetValue(key, out name) && !IsGeneric(name)) return name;
            return "HID 鼠标（VID:" + match.Groups[1].Value.ToUpperInvariant() +
                " PID:" + match.Groups[2].Value.ToUpperInvariant() + "）";
        }

        private static string Clean(string name)
        {
            return (name ?? "").Trim('\0', ' ', '\t', '\r', '\n');
        }

        private static bool IsGeneric(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return true;
            string compact = Regex.Replace(name, @"[\s\-_（）()]+", "").ToLowerInvariant();
            return compact == "mouse" || compact == "hidmouse" || compact == "hidcompliantmouse" ||
                compact == "usbmouse" || compact == "usbopticalmouse" || compact == "opticalmouse" ||
                compact == "usbgamingmouse" || compact == "gamingmouse" || compact == "wirelessmouse" ||
                compact == "2.4gwirelessmouse" || compact == "2.4ghzwirelessmouse" ||
                compact == "hid鼠标" || compact == "符合hid标准的鼠标" || compact == "usb鼠标" ||
                compact == "鼠标" || compact == "鼠标名称不可用";
        }

        private static Dictionary<string, string> LoadProducts()
        {
            var products = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("MouseTester.UsbIds"))
            {
                if (stream == null) return products;
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    string vendor = null, vendorName = null, line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (line.Length == 0 || line[0] == '#') continue;
                        if (line[0] != '\t')
                        {
                            vendor = null;
                            if (line.Length < 6 || !IsHexId(line.Substring(0, 4)) || !char.IsWhiteSpace(line[4])) continue;
                            vendor = line.Substring(0, 4).ToUpperInvariant();
                            vendorName = line.Substring(4).Trim();
                        }
                        else if (vendor != null && line.Length >= 7 && line[1] != '\t' &&
                            IsHexId(line.Substring(1, 4)) && char.IsWhiteSpace(line[5]))
                        {
                            string product = line.Substring(5).Trim();
                            if (product.Length == 0) continue;
                            string fullName = product;
                            if (vendorName != null && product.IndexOf(vendorName, StringComparison.OrdinalIgnoreCase) < 0)
                                fullName = vendorName + " — " + product;
                            products[vendor + ":" + line.Substring(1, 4).ToUpperInvariant()] = fullName;
                        }
                    }
                }
            }
            return products;
        }

        private static bool IsHexId(string text)
        {
            foreach (char c in text)
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'))) return false;
            return text.Length == 4;
        }
    }
}

