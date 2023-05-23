// Copyright 2023 Yubico AB
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

using Yubico.YubiKey.Fido2.PinProtocols;
using Yubico.YubiKey.TestUtilities;
using Xunit;
using System.Collections.Generic;

namespace Yubico.YubiKey.Fido2.Commands
{
    public class ConfigCommandTests : NeedPinToken
    {
        public ConfigCommandTests()
            : base(YubiKeyApplication.Fido2, StandardTestDevice.Bio, null)
        {
        }

        [Fact]
        public void EnableEnterpriseAttestationCommand_Succeeds()
        {
            var infoCmd = new GetInfoCommand();
            GetInfoResponse infoRsp = Connection.SendCommand(infoCmd);
            Assert.Equal(ResponseStatus.Success, infoRsp.Status);
            AuthenticatorInfo authInfo = infoRsp.GetData();
            Assert.NotNull(authInfo.Options);

            var protocol = new PinUvAuthProtocolTwo();
            bool isValid = GetPinToken(
                protocol, PinUvAuthTokenPermissions.AuthenticatorConfiguration, out byte[] pinToken);
            Assert.True(isValid);

            var cmd = new EnableEnterpriseAttestationCommand(pinToken, protocol);
            Fido2Response rsp = Connection.SendCommand(cmd);

            Assert.Equal(CtapStatus.Ok, rsp.CtapStatus);

            infoRsp = Connection.SendCommand(infoCmd);
            Assert.Equal(ResponseStatus.Success, infoRsp.Status);
            authInfo = infoRsp.GetData();
            Assert.NotNull(authInfo.Options);
        }

        [Fact]
        public void ToggleAlwaysUvCommand_Succeeds()
        {
            var infoCmd = new GetInfoCommand();
            GetInfoResponse infoRsp = Connection.SendCommand(infoCmd);
            Assert.Equal(ResponseStatus.Success, infoRsp.Status);
            AuthenticatorInfo authInfo = infoRsp.GetData();
            Assert.NotNull(authInfo.Options);

            var protocol = new PinUvAuthProtocolTwo();
            bool isValid = GetPinToken(
                protocol, PinUvAuthTokenPermissions.AuthenticatorConfiguration, out byte[] pinToken);
            Assert.True(isValid);

            var cmd = new ToggleAlwaysUvCommand(pinToken, protocol);
            Fido2Response rsp = Connection.SendCommand(cmd);

            Assert.Equal(CtapStatus.Ok, rsp.CtapStatus);

            infoRsp = Connection.SendCommand(infoCmd);
            Assert.Equal(ResponseStatus.Success, infoRsp.Status);
            authInfo = infoRsp.GetData();
            Assert.NotNull(authInfo.Options);
        }
    }
}
