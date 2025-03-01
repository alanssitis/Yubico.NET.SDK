﻿// Copyright 2021 Yubico AB
//
// Licensed under the Apache License, Version 2.0 (the "License").
// You may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Yubico.YubiKey
{
    /// <summary>
    /// This class can be used in commands to set and retrieve
    /// detailed device and capabilities information for a YubiKey.
    /// </summary>
    public class YubiKeyDeviceInfo : IYubiKeyDeviceInfo
    {
        private const byte UsbPrePersCapabilitiesTag = 0x01;
        private const byte SerialNumberTag = 0x02;
        private const byte UsbEnabledCapabilitiesTag = 0x03;
        private const byte FormFactorTag = 0x04;
        private const byte FirmwareVersionTag = 0x05;
        private const byte AutoEjectTimeoutTag = 0x06;
        private const byte ChallengeResponseTimeoutTag = 0x07;
        private const byte DeviceFlagsTag = 0x08;
        private const byte ConfigurationLockPresentTag = 0x0a;
        private const byte NfcPrePersCapabilitiesTag = 0x0d;
        private const byte NfcEnabledCapabilitiesTag = 0x0e;
        private const byte MoreDataTag = 0x10;
        private const byte FreeFormTag = 0x11;
        private const byte HidInitDelay = 0x12;
        private const byte PartNumberTag = 0x13;
        private const byte FipsCapableTag = 0x14;
        private const byte FipsApprovedTag = 0x15;
        private const byte PinComplexityTag = 0x16;
        private const byte NfcRestrictedTag = 0x17;
        private const byte ResetBlockedTag = 0x18;
        private const byte TemplateStorageVersionTag = 0x20;
        private const byte ImageProcessorVersionTag = 0x21;

        // The IapFlags tag may be returned by the device, but it should be ignored.
        private const byte IapDetectionTag = 0x0f;

        private const byte FipsMask = 0b1000_0000;
        private const byte SkyMask = 0b0100_0000;
        private const byte FormFactorMask = unchecked((byte)~(FipsMask | SkyMask));

        private static readonly FirmwareVersion _fipsInclusiveLowerBound =
            FirmwareVersion.V4_4_0;

        private static readonly FirmwareVersion _fipsExclusiveUpperBound =
            FirmwareVersion.V4_5_0;

        private static readonly FirmwareVersion _fipsFlagInclusiveLowerBound =
            FirmwareVersion.V5_4_2;

        /// <inheritdoc />
        public YubiKeyCapabilities AvailableUsbCapabilities { get; set; }

        /// <inheritdoc />
        public YubiKeyCapabilities EnabledUsbCapabilities { get; set; }

        /// <inheritdoc />
        public YubiKeyCapabilities AvailableNfcCapabilities { get; set; }

        /// <inheritdoc />
        public YubiKeyCapabilities EnabledNfcCapabilities { get; set; }

        /// <inheritdoc />
        public int? SerialNumber { get; set; }

        /// <inheritdoc />
        public bool IsFipsSeries { get; set; }

        /// <inheritdoc />
        public bool IsSkySeries { get; set; }

        /// <inheritdoc />
        public FormFactor FormFactor { get; set; }

        /// <inheritdoc />
        public FirmwareVersion FirmwareVersion { get; set; }

        /// <inheritdoc />
        public TemplateStorageVersion? TemplateStorageVersion { get; set; }

        /// <inheritdoc />
        public ImageProcessorVersion? ImageProcessorVersion { get; set; }

        /// <inheritdoc />
        public int AutoEjectTimeout { get; set; }

        /// <inheritdoc />
        public byte ChallengeResponseTimeout { get; set; }

        /// <inheritdoc />
        public DeviceFlags DeviceFlags { get; set; }

        /// <inheritdoc />
        public bool ConfigurationLocked { get; set; }

        /// <inheritdoc />
        public bool IsNfcRestricted { get; set; }

        /// <summary>
        /// Constructs a default instance of YubiKeyDeviceInfo.
        /// </summary>
        public YubiKeyDeviceInfo()
        {
            FirmwareVersion = new FirmwareVersion();
        }

        /// <summary>
        /// Gets the YubiKey's device configuration details.
        /// </summary>
        /// <param name="responseApduData">
        /// The ResponseApdu data as byte array returned by the YubiKey. The first byte
        /// is the overall length of the TLV data, followed by the TLV data.
        /// </param>
        /// <param name="deviceInfo">
        /// On success, the <see cref="YubiKeyDeviceInfo"/> is returned using this out parameter.
        /// </param>
        /// <returns>
        /// True if the parsing and construction was successful, and false if
        /// <paramref name="responseApduData"/> did not meet formatting requirements.
        /// </returns>
        [Obsolete("This has been replaced by CreateFromResponseData")]
        internal static bool TryCreateFromResponseData(
            ReadOnlyMemory<byte> responseApduData,
            [MaybeNullWhen(returnValue: false)] out YubiKeyDeviceInfo deviceInfo)
        {
            Dictionary<int, ReadOnlyMemory<byte>>? data =
                GetDeviceInfoResponseHelper.CreateApduDictionaryFromResponseData(responseApduData);

            if (data is null)
            {
                deviceInfo = null;

                return false;
            }

            deviceInfo = CreateFromResponseData(data);

            return true;
        }

        /// <summary>
        /// Gets the YubiKey's device configuration details.
        /// </summary>
        /// <param name="responseApduData">
        /// The ResponseApdu data as byte array returned by the YubiKey. The first byte
        /// is the overall length of the TLV data, followed by the TLV data.
        /// </param>
        /// <returns>
        /// On success, the <see cref="YubiKeyDeviceInfo"/> is returned using this out parameter.
        /// <paramref name="responseApduData"/> did not meet formatting requirements.
        /// </returns>
        internal static YubiKeyDeviceInfo CreateFromResponseData(Dictionary<int, ReadOnlyMemory<byte>> responseApduData)
        {
            bool fipsSeriesFlag = false;
            bool skySeriesFlag = false;

            var deviceInfo = new YubiKeyDeviceInfo();

            foreach (KeyValuePair<int, ReadOnlyMemory<byte>> tagValuePair in responseApduData)
            {
                switch (tagValuePair.Key)
                {
                    case UsbPrePersCapabilitiesTag:
                        deviceInfo.AvailableUsbCapabilities = GetYubiKeyCapabilities(tagValuePair.Value.Span);

                        break;

                    case SerialNumberTag:
                        deviceInfo.SerialNumber = BinaryPrimitives.ReadInt32BigEndian(tagValuePair.Value.Span);

                        break;

                    case UsbEnabledCapabilitiesTag:
                        deviceInfo.EnabledUsbCapabilities = GetYubiKeyCapabilities(tagValuePair.Value.Span);

                        break;

                    case FormFactorTag:
                        byte formFactorValue = tagValuePair.Value.Span[0];
                        deviceInfo.FormFactor = (FormFactor)(formFactorValue & FormFactorMask);
                        fipsSeriesFlag = (formFactorValue & FipsMask) == FipsMask;
                        skySeriesFlag = (formFactorValue & SkyMask) == SkyMask;

                        break;

                    case FirmwareVersionTag:
                        ReadOnlySpan<byte> firmwareValue = tagValuePair.Value.Span;

                        deviceInfo.FirmwareVersion = new FirmwareVersion
                        {
                            Major = firmwareValue[0],
                            Minor = firmwareValue[1],
                            Patch = firmwareValue[2]
                        };

                        break;

                    case AutoEjectTimeoutTag:
                        deviceInfo.AutoEjectTimeout = BinaryPrimitives.ReadUInt16BigEndian(tagValuePair.Value.Span);

                        break;

                    case ChallengeResponseTimeoutTag:
                        deviceInfo.ChallengeResponseTimeout = tagValuePair.Value.Span[0];

                        break;

                    case DeviceFlagsTag:
                        deviceInfo.DeviceFlags = (DeviceFlags)tagValuePair.Value.Span[0];

                        break;

                    case ConfigurationLockPresentTag:
                        deviceInfo.ConfigurationLocked = tagValuePair.Value.Span[0] == 1;

                        break;

                    case NfcPrePersCapabilitiesTag:
                        deviceInfo.AvailableNfcCapabilities = GetYubiKeyCapabilities(tagValuePair.Value.Span);

                        break;

                    case NfcEnabledCapabilitiesTag:
                        deviceInfo.EnabledNfcCapabilities = GetYubiKeyCapabilities(tagValuePair.Value.Span);

                        break;

                    case TemplateStorageVersionTag:
                        ReadOnlySpan<byte> fpChipVersion = tagValuePair.Value.Span;

                        deviceInfo.TemplateStorageVersion = new TemplateStorageVersion
                        {
                            Major = fpChipVersion[0],
                            Minor = fpChipVersion[1],
                            Patch = fpChipVersion[2]
                        };

                        break;

                    case ImageProcessorVersionTag:
                        ReadOnlySpan<byte> ipChipVersion = tagValuePair.Value.Span;

                        deviceInfo.ImageProcessorVersion = new ImageProcessorVersion
                        {
                            Major = ipChipVersion[0],
                            Minor = ipChipVersion[1],
                            Patch = ipChipVersion[2]
                        };

                        break;

                    case NfcRestrictedTag:
                        deviceInfo.IsNfcRestricted = tagValuePair.Value.Span[0] == 1;

                        break;
                    case IapDetectionTag:
                    case MoreDataTag:
                    case PartNumberTag:
                    case FipsCapableTag:
                    case FipsApprovedTag:
                    case PinComplexityTag:
                    case FreeFormTag:
                    case HidInitDelay:
                    case ResetBlockedTag:
                        // Ignore these tags for now
                        break;

                    default:
                        Debug.Assert(false, "Encountered an unrecognized tag in DeviceInfo. Ignoring.");

                        break;
                }
            }

            deviceInfo.IsFipsSeries = deviceInfo.FirmwareVersion >= _fipsFlagInclusiveLowerBound
                ? fipsSeriesFlag
                : deviceInfo.IsFipsVersion;

            deviceInfo.IsSkySeries |= skySeriesFlag;

            return deviceInfo;
        }

        internal YubiKeyDeviceInfo Merge(YubiKeyDeviceInfo? second)
        {
            second ??= new YubiKeyDeviceInfo();

            return new YubiKeyDeviceInfo
            {
                AvailableUsbCapabilities = AvailableUsbCapabilities | second.AvailableUsbCapabilities,

                EnabledUsbCapabilities = EnabledUsbCapabilities | second.EnabledUsbCapabilities,

                AvailableNfcCapabilities = AvailableNfcCapabilities | second.AvailableNfcCapabilities,

                EnabledNfcCapabilities = EnabledNfcCapabilities | second.EnabledNfcCapabilities,

                SerialNumber = SerialNumber ?? second.SerialNumber,

                IsFipsSeries = IsFipsSeries || second.IsFipsSeries,

                FormFactor = FormFactor != FormFactor.Unknown
                    ? FormFactor
                    : second.FormFactor,

                FirmwareVersion = FirmwareVersion != new FirmwareVersion()
                    ? FirmwareVersion
                    : second.FirmwareVersion,

                AutoEjectTimeout = DeviceFlags.HasFlag(DeviceFlags.TouchEject)
                    ? AutoEjectTimeout
                    : second.DeviceFlags.HasFlag(DeviceFlags.TouchEject)
                        ? second.AutoEjectTimeout
                        : default,

                ChallengeResponseTimeout = ChallengeResponseTimeout != default
                    ? ChallengeResponseTimeout
                    : second.ChallengeResponseTimeout,

                DeviceFlags = DeviceFlags | second.DeviceFlags,

                ConfigurationLocked = ConfigurationLocked != default
                    ? ConfigurationLocked
                    : second.ConfigurationLocked,

                IsNfcRestricted = IsNfcRestricted || second.IsNfcRestricted
            };
        }

        /// <summary>
        /// The firmware version is known to be a FIPS Series device.
        /// </summary>
        private bool IsFipsVersion =>
            FirmwareVersion >= _fipsInclusiveLowerBound
            && FirmwareVersion < _fipsExclusiveUpperBound;

        private static YubiKeyCapabilities GetYubiKeyCapabilities(ReadOnlySpan<byte> value) =>

            // Older firmware versions only encode this enumeration with one byte. If we see a single
            // byte in this element, it's OK. We will handle it here.
            value.Length == 1
                ? (YubiKeyCapabilities)value[0]
                : (YubiKeyCapabilities)BinaryPrimitives.ReadInt16BigEndian(value);

        /// <inheritdoc/>
        public override string ToString() =>
            $"{nameof(AvailableUsbCapabilities)}: {AvailableUsbCapabilities}, {nameof(EnabledUsbCapabilities)}: {EnabledUsbCapabilities}, {nameof(AvailableNfcCapabilities)}: {AvailableNfcCapabilities}, {nameof(EnabledNfcCapabilities)}: {EnabledNfcCapabilities}, {nameof(AutoEjectTimeout)}: {AutoEjectTimeout}, {nameof(DeviceFlags)}: {DeviceFlags}, {nameof(ConfigurationLocked)}: {ConfigurationLocked}";
    }
}
