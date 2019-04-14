﻿using System;
using Marten.Services;
using Marten.Util;
using NodaTime;
using NodaTime.Serialization.JsonNet;
using Npgsql;

namespace Marten.NodaTime
{
    public static class NodaTimeExtensions
    {
        /// <summary>
        /// Sets up NodaTime mappings for the PostgreSQL date/time types.
        ///
        /// By setting up NodaTime mappings - you're opting out of DateTime type handling. Using DateTime in your Document will end up getting NotSupportedException exception.
        /// </summary>
        /// <param name="storeOptions">store options that you're extending</param>
        /// <param name="shouldConfigureJsonNetSerializer">sets if NodaTime configuration should be setup for JsonNetSerializer. Set value to false if you're using different serializer type or you'd like to maintain your own configuration.</param>
        public static void UseNodaTime(this StoreOptions storeOptions, bool shouldConfigureJsonNetSerializer = true)
        {
            NpgsqlConnection.GlobalTypeMapper.UseNodaTime();
            TypeMappings.CustomDateTimeMapping = CustomDateTimeMapping;
            TypeMappings.CustomDateTimeOffsetMapping = CustomDateTimeOffsetMapping;

            if (shouldConfigureJsonNetSerializer)
            {
                var serializer = storeOptions.Serializer();
                (serializer as JsonNetSerializer)?.Customize(s => s.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb));
                storeOptions.Serializer(serializer);
            }
        }

        private static DateTime CustomDateTimeMapping(object value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            if (value is DateTime)
                return (DateTime)value;
            if (value is Instant)
                return ((Instant)value).ToDateTimeUtc();

            throw new ArgumentException($"Cannot convert type {value?.GetType()} to DateTime", nameof(value));
        }

        private static DateTimeOffset CustomDateTimeOffsetMapping(object value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            if (value is DateTimeOffset)
                return (DateTimeOffset)value;
            if (value is Instant)
                return ((Instant)value).ToDateTimeOffset();

            throw new ArgumentException($"Cannot convert type {value?.GetType()} to DateTimeOffset", nameof(value));
        }
    }
}