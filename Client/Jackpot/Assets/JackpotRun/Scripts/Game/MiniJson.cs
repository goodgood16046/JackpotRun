using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace JackpotRun.Game
{
    // 순수 C# JSON 파서(파싱 전용, 쓰기 없음) — ENGINE_PORT_DESIGN.md S15 "MiniJson.cs": RTDB REST
    // GET 응답(RankingService.Fetch)을 읽기 위한 RankingService 전용 최소 구현이다. UnityEngine
    // 비의존(이 파일만은 예외적으로 Scripts/Game 안에서도 순수 C#을 유지 — Engine/**와 같은 이유로
    // 별도 프레임워크 의존 없이 재사용 가능해야 하기 때문). object → Dictionary<string, object>
    // (object) · List<object>(array) · string · double(number) · bool · null 로 변환한다. 형식
    // 오류가 나면 예외를 던지는 대신 null을 반환한다(호출측이 오류로 취급).
    public static class MiniJson
    {
        public static object Parse(string json)
        {
            if (json == null) return null;
            try
            {
                int i = 0;
                SkipWhitespace(json, ref i);
                object result = ParseValue(json, ref i);
                SkipWhitespace(json, ref i);
                if (i != json.Length) return null; // 트레일링 문자 — 형식 오류
                return result;
            }
            catch (Exception)
            {
                // FormatException/IndexOutOfRange 외에도 double.Parse의 OverflowException(Mono) 등
                // 어떤 형식 오류든 null로 수렴시킨다 — 예외가 새면 호출측(FetchRoutine) 코루틴이
                // 중단돼 onOk/onError 둘 다 안 불리는 영구 로딩이 되기 때문(Opus S15 검수 경미-4).
                return null;
            }
        }

        private static object ParseValue(string s, ref int i)
        {
            if (i >= s.Length) throw new FormatException("unexpected end of input");
            char c = s[i];
            switch (c)
            {
                case '{': return ParseObject(s, ref i);
                case '[': return ParseArray(s, ref i);
                case '"': return ParseString(s, ref i);
                case 't': Expect(s, ref i, "true"); return true;
                case 'f': Expect(s, ref i, "false"); return false;
                case 'n': Expect(s, ref i, "null"); return null;
                default: return ParseNumber(s, ref i);
            }
        }

        private static Dictionary<string, object> ParseObject(string s, ref int i)
        {
            var dict = new Dictionary<string, object>();
            ExpectChar(s, ref i, '{');
            SkipWhitespace(s, ref i);
            if (Peek(s, i) == '}') { i++; return dict; }

            while (true)
            {
                SkipWhitespace(s, ref i);
                if (Peek(s, i) != '"') throw new FormatException("expected string key");
                string key = ParseString(s, ref i);
                SkipWhitespace(s, ref i);
                ExpectChar(s, ref i, ':');
                SkipWhitespace(s, ref i);
                object value = ParseValue(s, ref i);
                dict[key] = value;
                SkipWhitespace(s, ref i);
                char next = Peek(s, i);
                if (next == ',') { i++; continue; }
                if (next == '}') { i++; break; }
                throw new FormatException("expected ',' or '}'");
            }
            return dict;
        }

        private static List<object> ParseArray(string s, ref int i)
        {
            var list = new List<object>();
            ExpectChar(s, ref i, '[');
            SkipWhitespace(s, ref i);
            if (Peek(s, i) == ']') { i++; return list; }

            while (true)
            {
                SkipWhitespace(s, ref i);
                list.Add(ParseValue(s, ref i));
                SkipWhitespace(s, ref i);
                char next = Peek(s, i);
                if (next == ',') { i++; continue; }
                if (next == ']') { i++; break; }
                throw new FormatException("expected ',' or ']'");
            }
            return list;
        }

        private static string ParseString(string s, ref int i)
        {
            ExpectChar(s, ref i, '"');
            var sb = new StringBuilder();
            while (true)
            {
                if (i >= s.Length) throw new FormatException("unterminated string");
                char c = s[i++];
                if (c == '"') break;
                if (c == '\\')
                {
                    if (i >= s.Length) throw new FormatException("unterminated escape");
                    char esc = s[i++];
                    switch (esc)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (i + 4 > s.Length) throw new FormatException("bad \\u escape");
                            string hex = s.Substring(i, 4);
                            i += 4;
                            sb.Append((char)ushort.Parse(hex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture));
                            break;
                        default:
                            throw new FormatException("bad escape char");
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        private static double ParseNumber(string s, ref int i)
        {
            int start = i;
            if (Peek(s, i) == '-') i++;
            if (i >= s.Length || !IsDigit(s[i])) throw new FormatException("expected digit");
            while (i < s.Length && IsDigit(s[i])) i++;
            if (Peek(s, i) == '.')
            {
                i++;
                if (i >= s.Length || !IsDigit(s[i])) throw new FormatException("expected digit after '.'");
                while (i < s.Length && IsDigit(s[i])) i++;
            }
            if (Peek(s, i) == 'e' || Peek(s, i) == 'E')
            {
                i++;
                if (Peek(s, i) == '+' || Peek(s, i) == '-') i++;
                if (i >= s.Length || !IsDigit(s[i])) throw new FormatException("expected digit in exponent");
                while (i < s.Length && IsDigit(s[i])) i++;
            }
            string token = s.Substring(start, i - start);
            return double.Parse(token, NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        private static bool IsDigit(char c) => c >= '0' && c <= '9';

        private static char Peek(string s, int i) => i < s.Length ? s[i] : '\0';

        private static void ExpectChar(string s, ref int i, char expected)
        {
            if (i >= s.Length || s[i] != expected) throw new FormatException($"expected '{expected}'");
            i++;
        }

        private static void Expect(string s, ref int i, string literal)
        {
            if (i + literal.Length > s.Length || string.CompareOrdinal(s, i, literal, 0, literal.Length) != 0)
                throw new FormatException($"expected '{literal}'");
            i += literal.Length;
        }

        private static void SkipWhitespace(string s, ref int i)
        {
            while (i < s.Length)
            {
                char c = s[i];
                if (c == ' ' || c == '\t' || c == '\n' || c == '\r') i++;
                else break;
            }
        }
    }
}
