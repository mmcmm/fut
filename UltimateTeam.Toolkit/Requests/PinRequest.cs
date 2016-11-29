using System.Collections.Generic;
using System.Dynamic;
using System.Net.Http;
using System.Threading.Tasks;
using UltimateTeam.Toolkit.Constants;
using UltimateTeam.Toolkit.Models;
using UltimateTeam.Toolkit.Extensions;
using System;
using UltimateTeam.Toolkit.Exceptions;

namespace UltimateTeam.Toolkit.Requests
{
    internal class PinEventRequest : FutRequestBase, IFutRequest<PinResponse>
    {
        private readonly AppVersion _appVersion;
        private readonly string _nucleusId;
        private readonly string _personaId;
        private readonly string _sessionId;
        private readonly string _currentPinEventIdString;
        private readonly string _previousPinEventIdString;
        private PinEventId _currentPinEventId;
        private readonly uint _pinRequestCount;
        private readonly Platform _platform;

        public PinEventRequest(AppVersion appVersion, string SessionId, string NucleusId, string PersonaId, PinEventId currentPinId, PinEventId previousPinId, uint pinRequestCount, Platform platform)
        {
            _appVersion = appVersion;
            _sessionId = SessionId;
            _nucleusId = NucleusId;
            _personaId = PersonaId;
            _currentPinEventIdString = ObjectExtensions.DataContractSerializeObject(currentPinId);
            _previousPinEventIdString = ObjectExtensions.DataContractSerializeObject(previousPinId);
            _currentPinEventId = currentPinId;
            _pinRequestCount = pinRequestCount;
            _platform = platform;
        }

        public async Task<PinResponse> PerformRequestAsync()
        {
            if (AppVersion != AppVersion.WebApp)
            {
                AddPinHeadersMobile();
            }
            else
            {
                AddPinHeaders();
            }
            var pinResponseMessage = await HttpClient.PostAsync(string.Format(Resources.PinRiver), new StringContent(Serialize(GeneratePinData())))
                .ConfigureAwait(false);

            return await DeserializeAsync<PinResponse>(pinResponseMessage);
        }

        private ExpandoObject GeneratePinData()
        {
            List<object> pinDataEvents = new List<object>();
            dynamic pinData = new ExpandoObject();
            dynamic pinDataEventsCore = new ExpandoObject();
            dynamic pinDataEventsCorePidm = new ExpandoObject();
            dynamic pinDataCustom = new ExpandoObject();
            dynamic pinDataEvent = new ExpandoObject();

            pinData.custom = pinDataCustom;
            pinDataEventsCore.pidm = pinDataEventsCorePidm;
            pinDataEvent.core = pinDataEventsCore;
            pinDataEvents.Add(pinDataEvent);
            pinData.events = pinDataEvents;

            pinData.et = "client";
            pinData.loc = "en_GB";
            pinData.rel = "prod";
            pinData.sid = _sessionId;
            pinData.ts_post = DateTimeExtensions.ToISO8601Time(DateTime.UtcNow);

            pinDataEvent.type = "menu";
            pinDataEventsCore.en = "page_view";
            pinDataEventsCore.pid = _personaId;
            pinDataEventsCorePidm.nucleus = _nucleusId;
            pinDataEventsCore.pidt = "persona";
            pinDataEventsCore.s = _pinRequestCount;
            pinDataEventsCore.ts_event = DateTimeExtensions.ToISO8601Time(DateTime.UtcNow);

            if (_appVersion == AppVersion.CompanionApp)
            {
                pinDataCustom.networkAccess = "W";
                pinDataCustom.service_plat = GetServicePlat(_platform).ToLower();
                pinData.taxv = 1.1;
                pinData.tid = "874217";
                pinData.tidt = "sellid";
                pinData.gid = 0;
                pinData.plat = "android";
                pinData.v = "17.0.0.162442";

                if (_currentPinEventId == PinEventId.CompanionApp_AppOpened)
                {
                    pinData.events = pinDataEventsCore;
                    pinDataEventsCore.en = "connection";
                    pinDataEventsCore.pid = new ExpandoObject();
                    pinDataEventsCorePidm.nucleus = "0";
                    pinData.sid = new ExpandoObject();
                }
                else if (_currentPinEventId == PinEventId.CompanionApp_Connect)
                {
                    pinDataEventsCore.en = "login";
                    pinDataEventsCore.pid = new ExpandoObject();
                    pinDataEventsCorePidm.nucleus = _nucleusId;
                    pinDataEvent.status = "success";
                    pinDataEvent.type = "PAS";
                    pinData.sid = new ExpandoObject();
                }
                else if (_currentPinEventId == PinEventId.CompanionApp_Connected)
                {
                    pinDataEventsCore.en = "login";
                    pinDataEventsCore.pid = _personaId;
                    pinDataEventsCorePidm.nucleus = _nucleusId;
                    pinDataEvent.status = "success";
                    pinDataEvent.status = "UTAS";
                    pinData.sid = _sessionId;
                    pinData.userid = _personaId;
                }
                else
                {
                    pinDataEvent.pgid = _currentPinEventIdString;
                }
                return pinData;
            }
            else if (_appVersion == AppVersion.WebApp)
            {
                pinDataCustom.service_plat = GetServicePlat(_platform);
                pinData.taxv = 1.1;
                pinData.tid = "FUT17WEB";
                pinData.tidt = "sku";
                pinData.plat = "web";
                pinData.v = "17.0.164470";
                pinDataEvent.custom = new ExpandoObject();


                if (_currentPinEventId == PinEventId.WebApp_Home)
                {
                    pinDataEventsCore.en = "login";
                    pinDataEventsCore.pid = _personaId;
                    pinDataEventsCorePidm.nucleus = _nucleusId;
                    pinDataEvent.status = "success";
                    pinDataEvent.status = "nucleus";
                    pinData.sid = _sessionId;
                    pinData.userid = _personaId;
                }
                else
                {
                    pinDataEvent.pgid = _previousPinEventIdString.ToLower();
                    pinDataEvent.toid = _currentPinEventIdString.ToLower();
                }

                return pinData;
            }
            else
            {
                throw new FutException(string.Format("Unknown AppVersion: {0}", _appVersion.ToString()));
            }
        }

        private static string GetServicePlat(Platform platform)
        {
            switch (platform)
            {
                case Platform.Ps3:
                    return "PS3";
                case Platform.Ps4:
                    return "PS4";
                case Platform.Xbox360:
                    return "XBX";
                case Platform.XboxOne:
                    return "XBO";
                case Platform.Pc:
                    return "PCC";
                default:
                    throw new ArgumentOutOfRangeException(nameof(platform), platform, null);
            }
        }
    }
}