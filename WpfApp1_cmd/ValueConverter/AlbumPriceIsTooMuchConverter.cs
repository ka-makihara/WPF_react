﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using WpfApp1_cmd.Models;

namespace WpfApp1_cmd.ValueConverter
{
    public class AlbumPriceIsTooMuchConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal)
            {
                var price = (decimal)value;
                if (price > 15 && price < 20)
                {
                    return true;
                }
            }

            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return DependencyProperty.UnsetValue;
        }
    }

    public class AlbumPriceIsReallyTooMuchValidation : ValidationRule
    {
        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            var bindingGroup = value as BindingGroup;
            var album = bindingGroup?.Items.OfType<Album>().ElementAtOrDefault(0);
            if (album != null && album.Price >= 20)
            {
                return new ValidationResult(false, $"The price {album.Price} of the album '{album.Title}' by '{album.Artist?.Name}' is too much!");
            }

            return ValidationResult.ValidResult;
        }
    }
}