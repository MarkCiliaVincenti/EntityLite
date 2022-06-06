﻿/*
Copyright 2014 i-nercya intelligent software

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace inercya.EntityLite.Extensions
{
    public static class StringExtensions
    {
        
        public static string ToPascalNamingConvention(this string str)
        {
            if (str == null) return null;
            StringBuilder sb = new StringBuilder(str.Length);
            bool isPreviousLower = false;
            bool isNewWord = true;
            for (int i = 0; i < str.Length; i++)
            {
                char c = str[i];
                if (c == ' ' || c == '_')
                {
                    isNewWord = true;
                    continue;
                }
                bool isUpper = char.IsUpper(c);
                bool isLower = char.IsLower(c);
                bool isNextLower = i < str.Length -1 &&  char.IsLower(str[i+1]);

                if ( isUpper && (isPreviousLower || isNextLower))
                {
                    isNewWord = true;
                }
                if (isNewWord)
                {
                    isNewWord = false;
                    if (isLower) c = char.ToUpper(c, CultureInfo.InvariantCulture);
                }
                else
                {
                    if (isUpper) c = char.ToLower(c, CultureInfo.InvariantCulture);
                }
                isPreviousLower = isLower;
                sb.Append(c);
            }
            return sb.ToString();
        }


        public static string ToUnderscoreLowerCaseNamingConvention(this string str)
        {
            if (str == null) return null;
            StringBuilder sb = new StringBuilder(str.Length + 6);
            bool isPreviousCharLowerCase = false;
            for (int i = 0; i < str.Length; i++ )
            {
                char c = str[i];
                bool isUpper = char.IsUpper(c);
                if (isPreviousCharLowerCase && isUpper)
                {
                    sb.Append('_');
                }
                if (c == ' ') c = '_';
                isPreviousCharLowerCase = char.IsLower(c);
                if (isUpper) c = char.ToLower(c, CultureInfo.InvariantCulture);
                sb.Append(c);
            }
            return sb.ToString();
        }

        public static string ToUnderscoreUpperCaseNamingConvention(this string str)
        {
            if (str == null) return null;
            StringBuilder sb = new StringBuilder(str.Length + 8);
            bool isPreviousCharLowerCase = false;
            for (int i = 0; i < str.Length; i++)
            {
                char c = str[i];
                bool isLower = char.IsLower(c);
                if (isPreviousCharLowerCase && char.IsUpper(c))
                {
                    sb.Append('_');
                }
                if (c == ' ') c = '_';
                isPreviousCharLowerCase = isLower;
                if (isLower) c = char.ToUpper(c, CultureInfo.InvariantCulture);
                sb.Append(c);
            }
            return sb.ToString();
        }

        public static bool HasUpperAndLowerCaseChars(this string str)
        {
            if (str == null) return false;
            bool hasLower = false;
            bool hasUpper = false;
            foreach(char c in str)
            {
                if (!hasLower && char.IsLower(c)) hasLower = true;
                if (!hasUpper && char.IsUpper(c)) hasUpper = true;
                if (hasLower && hasUpper) return true;
            }
            return false;
        }

        public static string Transform(this string str, TextTransform textTransform)
        {
            if (str == null) return null;
            switch (textTransform)
            {
                case TextTransform.None:
                    return str;
                case TextTransform.ToLower:
                    return str.ToLowerInvariant();
                case TextTransform.ToUpper:
                    return str.ToUpperInvariant();
                case TextTransform.ToPascalNamingConvention:
                    return str.ToPascalNamingConvention();
                case TextTransform.ToUnderscoreLowerCaseNamingConvention:
                    return str.ToUnderscoreLowerCaseNamingConvention();
                case TextTransform.ToUnderscoreUpperCaseNamingConvention:
                    return str.ToUnderscoreUpperCaseNamingConvention();
                default:
                    throw new NotImplementedException();
            }
        }

        public static StringBuilder Indent(this StringBuilder builder, int indentation)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            var chars = indentation * 3;
            for(int i =0; i < chars; i++)
            {
                builder.Append(' ');
            }
            return builder;
        }

        public static StringBuilder NewIndentedLine(this StringBuilder builder, int indentation)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            return builder.Append('\n').Indent(indentation);
        }
    }
}
