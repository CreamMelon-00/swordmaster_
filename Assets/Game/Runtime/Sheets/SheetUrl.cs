using System;
using System.Collections.Generic;

namespace TurnLimbo.Runtime.Sheets
{
    /// <summary>Turns a Google Sheets address a designer copies from the browser into the address that downloads one tab
    /// as CSV. The sheet must be shared with anyone who has the link, or published to the web.</summary>
    public static class SheetUrl
    {
        private const string Host = "docs.google.com";
        private const string Help = "브라우저 주소창의 시트 주소(https://docs.google.com/spreadsheets/d/…/edit…)나 " +
            "파일 → 공유 → 웹에 게시에서 받은 주소를 붙여 넣으세요.";

        /// <summary>
        /// <list type="bullet">
        /// <item><c>…/spreadsheets/d/{ID}/edit…</c>: the tab from <c>#gid=</c> or <c>?gid=</c> (0 when absent) becomes
        /// <c>…/spreadsheets/d/{ID}/export?format=csv&amp;gid={GID}</c>.</item>
        /// <item>A published <c>…/spreadsheets/d/e/{PUBID}/pub…</c> (or <c>pubhtml</c>) keeps its gid and single
        /// parameters and gets <c>output=csv</c>.</item>
        /// <item>An <c>…/export?format=csv…</c> address is returned as it is (trimmed, and with <c>https://</c> when it
        /// was pasted without a scheme).</item>
        /// </list>
        /// Anything else fails with a Korean <paramref name="error"/>.</summary>
        public static bool TryGetCsvUrl(string input, out string csvUrl, out string error)
        {
            csvUrl = null;
            error = null;
            string text = input?.Trim() ?? string.Empty;
            if (text.Length == 0)
            {
                error = "구글 시트 주소를 붙여 넣으세요.";
                return false;
            }
            if (text.StartsWith(Host + "/", StringComparison.OrdinalIgnoreCase)) text = "https://" + text;
            if (!Uri.TryCreate(text, UriKind.Absolute, out Uri uri) ||
                (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) ||
                !string.Equals(uri.Host, Host, StringComparison.OrdinalIgnoreCase))
                return Fail($"구글 시트 주소가 아닙니다: '{input.Trim()}'. {Help}", out error);

            string[] path = uri.AbsolutePath.Trim('/').Split('/');
            // A browser signed in to several Google accounts puts the account in the path: /spreadsheets/u/1/d/….
            if (path.Length > 3 && path[0] == "spreadsheets" && path[1] == "u" && IsDigits(path[2]))
            {
                var withoutAccount = new string[path.Length - 2];
                withoutAccount[0] = path[0];
                Array.Copy(path, 3, withoutAccount, 1, path.Length - 3);
                path = withoutAccount;
            }
            List<KeyValuePair<string, string>> query = Parameters(uri.Query);
            List<KeyValuePair<string, string>> fragment = Parameters(uri.Fragment);
            if (path.Length < 3 || path[0] != "spreadsheets" || path[1] != "d")
                return Fail($"구글 시트 주소가 아닙니다: '{input.Trim()}'. {Help}", out error);

            // Published to the web: /spreadsheets/d/e/{PUBID}/pub or /pubhtml.
            if (path[2] == "e")
            {
                if (path.Length != 5 || !IsId(path[3]) || (path[4] != "pub" && path[4] != "pubhtml"))
                    return Fail($"웹에 게시한 시트 주소를 읽을 수 없습니다: '{input.Trim()}'. {Help}", out error);
                var parameters = new List<string>();
                string publishedGid = Value(query, "gid") ?? Value(fragment, "gid");
                if (publishedGid != null)
                {
                    if (!IsDigits(publishedGid)) return Fail(BadGid(publishedGid), out error);
                    parameters.Add("gid=" + publishedGid);
                }
                string single = Value(query, "single");
                if (single != null) parameters.Add("single=" + single);
                parameters.Add("output=csv");
                csvUrl = $"https://{Host}/spreadsheets/d/e/{path[3]}/pub?" + string.Join("&", parameters);
                return true;
            }

            if (!IsId(path[2])) return Fail($"구글 시트 주소의 문서 ID를 읽을 수 없습니다: '{input.Trim()}'. {Help}", out error);
            // Already a CSV download.
            if (path.Length == 4 && path[3] == "export")
            {
                if (!string.Equals(Value(query, "format"), "csv", StringComparison.OrdinalIgnoreCase))
                    return Fail("내보내기 주소가 CSV가 아닙니다. 주소에 format=csv가 있어야 합니다.", out error);
                // As pasted, with the https:// a bare docs.google.com/… address lacks, so the download address is absolute.
                csvUrl = text;
                return true;
            }
            // The editor: /spreadsheets/d/{ID}, /edit, /edit#gid=…, /edit?gid=…
            if (path.Length == 3 || (path.Length == 4 && path[3] == "edit"))
            {
                string gid = Value(fragment, "gid") ?? Value(query, "gid") ?? "0";
                if (!IsDigits(gid)) return Fail(BadGid(gid), out error);
                csvUrl = $"https://{Host}/spreadsheets/d/{path[2]}/export?format=csv&gid={gid}";
                return true;
            }
            return Fail($"이 구글 시트 주소에서는 CSV를 받을 수 없습니다: '{input.Trim()}'. {Help}", out error);
        }

        private static bool Fail(string message, out string error)
        {
            error = message;
            return false;
        }

        private static string BadGid(string gid) => $"시트 탭 번호(gid) '{gid}'을(를) 읽을 수 없습니다. 탭을 연 상태의 주소를 다시 복사하세요.";

        /// <summary>The key=value pairs of a query (<c>?…</c>) or fragment (<c>#…</c>), in order.</summary>
        private static List<KeyValuePair<string, string>> Parameters(string part)
        {
            var parameters = new List<KeyValuePair<string, string>>();
            string text = part.Length > 0 && (part[0] == '?' || part[0] == '#') ? part.Substring(1) : part;
            foreach (string pair in text.Split('&'))
            {
                if (pair.Length == 0) continue;
                int equals = pair.IndexOf('=');
                parameters.Add(equals < 0
                    ? new KeyValuePair<string, string>(pair, string.Empty)
                    : new KeyValuePair<string, string>(pair.Substring(0, equals), Uri.UnescapeDataString(pair.Substring(equals + 1))));
            }
            return parameters;
        }

        private static string Value(List<KeyValuePair<string, string>> parameters, string key)
        {
            foreach (KeyValuePair<string, string> parameter in parameters)
                if (parameter.Key == key) return parameter.Value;
            return null;
        }

        private static bool IsId(string value)
        {
            if (value.Length == 0) return false;
            foreach (char c in value)
                if (!(c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z' || c >= '0' && c <= '9' || c == '-' || c == '_')) return false;
            return true;
        }

        private static bool IsDigits(string value)
        {
            if (value.Length == 0) return false;
            foreach (char c in value)
                if (c < '0' || c > '9') return false;
            return true;
        }
    }
}
