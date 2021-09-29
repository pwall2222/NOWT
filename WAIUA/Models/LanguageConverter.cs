﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WAIUA.Models
{
    abstract class LanguageConverter
    {
        public static readonly Dictionary<string, string> ValApicomDictionary = new()
        {
            {"ar", "ar-AE"},
            {"de", "de-DE"},
            {"en", "en-US"},
            {"es", "es-ES"},
            {"fr", "fr-FR "},
            {"id", "id-ID"},
            {"it", "it-IT"},
            {"ja", "ja-JP"},
            {"ko", "ko-KR"},
            {"pl", "pl-PL"},
            {"pt", "pt-BR"},
            {"ru", "ru-RU"},
            {"th", "th-TH"},
            {"tr", "tr-TR"},
            {"vi", "vi-VN"},
            {"zh", "zh-CN"},
        };
    }
}