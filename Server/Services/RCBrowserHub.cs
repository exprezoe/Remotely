﻿using Remotely.Shared.Models;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Remotely.Shared.Enums;
using Microsoft.AspNetCore.Authorization;

namespace Remotely.Server.Services
{
    [Authorize("RemoteControlPolicy")]
    public class RCBrowserHub : Hub
    {
        public RCBrowserHub(DataService dataService,
            IHubContext<RCDeviceHub> rcDeviceHub,
            ApplicationConfig appConfig,
            RemoteControlSessionRecorder rcSessionRecorder)
        {
            DataService = dataService;
            RCDeviceHubContext = rcDeviceHub;
            AppConfig = appConfig;
            RCSessionRecorder = rcSessionRecorder;
        }
        private ApplicationConfig AppConfig { get; set; }
        private DataService DataService { get; }

        private RemoteControlMode Mode
        {
            get
            {
                return (RemoteControlMode)Context.Items["Mode"];
            }
            set
            {
                Context.Items["Mode"] = value;
            }
        }

        private IHubContext<RCDeviceHub> RCDeviceHubContext { get; }

        private RemoteControlSessionRecorder RCSessionRecorder { get; }

        private string RequesterName
        {
            get
            {
                return Context.Items["RequesterName"] as string;
            }
            set
            {
                Context.Items["RequesterName"] = value;
            }
        }

        private string ScreenCasterID
        {
            get
            {
                return Context.Items["ScreenCasterID"] as string;
            }
            set
            {
                Context.Items["ScreenCasterID"] = value;
            }
        }
        public Task CtrlAltDel()
        {
            return RCDeviceHubContext.Clients.Client(ScreenCasterID).SendAsync("CtrlAltDel", Context.ConnectionId);
        }

        public Task KeyDown(string key)
        {
            return RCDeviceHubContext.Clients.Client(ScreenCasterID).SendAsync("KeyDown", key, Context.ConnectionId);
        }

        public Task KeyPress(string key)
        {
            return RCDeviceHubContext.Clients.Client(ScreenCasterID).SendAsync("KeyPress", key, Context.ConnectionId);
        }

        public Task KeyUp(string key)
        {
            return RCDeviceHubContext.Clients.Client(ScreenCasterID).SendAsync("KeyUp", key, Context.ConnectionId);
        }

        public Task LongPress()
        {
            return RCDeviceHubContext.Clients.Client(ScreenCasterID).SendAsync("LongPress", Context.ConnectionId);
        }

        public Task MouseDown(int button, double percentX, double percentY)
        {
            return RCDeviceHubContext.Clients.Client(ScreenCasterID).SendAsync("MouseDown", button, percentX, percentY, Context.ConnectionId);
        }

        public Task MouseMove(double percentX, double percentY)
        {
            return RCDeviceHubContext.Clients.Client(ScreenCasterID).SendAsync("MouseMove", percentX, percentY, Context.ConnectionId);
        }

        public Task MouseUp(int button, double percentX, double percentY)
        {
            return RCDeviceHubContext.Clients.Client(ScreenCasterID).SendAsync("MouseUp", button, percentX, percentY, Context.ConnectionId);
        }

        public Task MouseWheel(double deltaX, double deltaY)
        {
            return RCDeviceHubContext.Clients.Client(ScreenCasterID).SendAsync("MouseWheel", deltaX, deltaY, Context.ConnectionId);
        }

        public override Task OnConnectedAsync()
        {
            return base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception exception)
        {
            if (ScreenCasterID != null)
            {
                RCDeviceHubContext.Clients.Client(ScreenCasterID).SendAsync("ViewerDisconnected", Context.ConnectionId);
            }

            if (AppConfig.RecordRemoteControlSessions)
            {
                RCSessionRecorder.StopProcessing(Context.ConnectionId);
            }

            return base.OnDisconnectedAsync(exception);
        }

        public Task SelectScreen(string displayName)
        {
            return RCDeviceHubContext.Clients.Client(ScreenCasterID).SendAsync("SelectScreen", displayName, Context.ConnectionId);
        }

        public Task SendAutoQualityAdjust(bool isOn)
        {
            return RCDeviceHubContext.Clients.Client(ScreenCasterID).SendAsync("AutoQualityAdjust", isOn, Context.ConnectionId);
        }

        public Task SendClipboardTransfer(string transferText, bool typeText)
        {
            return RCDeviceHubContext.Clients.Client(ScreenCasterID).SendAsync("ClipboardTransfer", transferText, typeText, Context.ConnectionId);
        }

        public Task SendFrameReceived(int bytesReceived)
        {
            return RCDeviceHubContext.Clients.Client(ScreenCasterID).SendAsync("FrameReceived", bytesReceived, Context.ConnectionId);
        }
        public Task SendIceCandidateToAgent(string candidate, int sdpMlineIndex, string sdpMid)
        {
            return RCDeviceHubContext.Clients.Client(ScreenCasterID).SendAsync("ReceiveIceCandidate", candidate, sdpMlineIndex, sdpMid, Context.ConnectionId);
        }

        public Task SendQualityChange(int qualityLevel)
        {
            return RCDeviceHubContext.Clients.Client(ScreenCasterID).SendAsync("QualityChange", qualityLevel, Context.ConnectionId);
        }
        public Task SendRtcAnswerToAgent(string sdp)
        {
            return RCDeviceHubContext.Clients.Client(ScreenCasterID).SendAsync("ReceiveRtcAnswer", sdp, Context.ConnectionId);
        }

        public async Task<Task> SendScreenCastRequestToDevice(string screenCasterID, string requesterName, int remoteControlMode)
        {
            if ((RemoteControlMode)remoteControlMode == RemoteControlMode.Normal)
            {
                if (!Services.RCDeviceHub.SessionInfoList.Any(x => x.Value.AttendedSessionID == screenCasterID))
                {
                    return Clients.Caller.SendAsync("SessionIDNotFound");
                }

                screenCasterID = Services.RCDeviceHub.SessionInfoList.First(x => x.Value.AttendedSessionID == screenCasterID).Value.RCDeviceSocketID;
            }

            if (!Services.RCDeviceHub.SessionInfoList.TryGetValue(screenCasterID, out var sessionInfo))
            {
                return Clients.Caller.SendAsync("SessionIDNotFound");
            }

            ScreenCasterID = screenCasterID;
            RequesterName = requesterName;
            Mode = (RemoteControlMode)remoteControlMode;

            string orgId = null;

            if (Context?.User?.Identity?.IsAuthenticated == true)
            {
                orgId = DataService.GetUserByID(Context.UserIdentifier).OrganizationID;
                var currentUsers = Services.RCDeviceHub.SessionInfoList.Count(x => 
                    x.Key != screenCasterID &&
                    x.Value.OrganizationID == orgId);
                if (currentUsers >= AppConfig.RemoteControlSessionLimit)
                {
                    await Clients.Caller.SendAsync("ShowMessage", "Max number of concurrent sessions reached.");
                    Context.Abort();
                    return Task.CompletedTask;
                }
                sessionInfo.OrganizationID = orgId;
                sessionInfo.RequesterUserName = Context.User.Identity.Name;
                sessionInfo.RequesterSocketID = Context.ConnectionId;
            }

            DataService.WriteEvent(new EventLog()
            {
                EventType = EventType.Info,
                TimeStamp = DateTimeOffset.Now,
                Message = $"Remote control session requested.  " +
                                $"Login ID (if logged in): {Context?.User?.Identity?.Name}.  " +
                                $"Machine Name: {sessionInfo.MachineName}.  " +
                                $"Requester Name (if specified): {requesterName}.  " +
                                $"Connection ID: {Context.ConnectionId}. User ID: {Context.UserIdentifier}.  " +
                                $"Screen Caster ID: {screenCasterID}.  " + 
                                $"Mode: {(RemoteControlMode)remoteControlMode}.  " + 
                                $"Requester IP Address: " + Context?.GetHttpContext()?.Connection?.RemoteIpAddress?.ToString(),
                OrganizationID = orgId
            });

            if (Mode == RemoteControlMode.Unattended)
            {
                sessionInfo.Mode = RemoteControlMode.Unattended;
                var deviceID = DeviceHub.ServiceConnections[sessionInfo.ServiceID].ID;
                if (Context.User.Identity.IsAuthenticated && DataService.DoesUserHaveAccessToDevice(deviceID, Context.UserIdentifier))
                {
                    return RCDeviceHubContext.Clients.Client(screenCasterID).SendAsync("GetScreenCast", Context.ConnectionId, requesterName);
                }
                else
                {
                    return Clients.Caller.SendAsync("Unauthorized");
                }
            }
            else
            {
                sessionInfo.Mode = RemoteControlMode.Normal;
                _ = Clients.Caller.SendAsync("RequestingScreenCast");
                return RCDeviceHubContext.Clients.Client(screenCasterID).SendAsync("RequestScreenCast", Context.ConnectionId, requesterName);
            }
        }
        public Task SendSharedFileIDs(List<string> fileIDs)
        {
            return RCDeviceHubContext.Clients.Client(ScreenCasterID).SendAsync("SharedFileIDs", fileIDs);
        }
        public Task SendToggleAudio(bool toggleOn)
        {
            return RCDeviceHubContext.Clients.Client(ScreenCasterID).SendAsync("ToggleAudio", toggleOn, Context.ConnectionId);
        }
        public Task SendToggleBlockInput(bool toggleOn)
        {
            return RCDeviceHubContext.Clients.Client(ScreenCasterID).SendAsync("ToggleBlockInput", toggleOn, Context.ConnectionId);
        }
        public Task Tap(double percentX, double percentY)
        {
            return RCDeviceHubContext.Clients.Client(ScreenCasterID).SendAsync("Tap", percentX, percentY, Context.ConnectionId);
        }

        public Task TouchDown()
        {
            return RCDeviceHubContext.Clients.Client(ScreenCasterID).SendAsync("TouchDown", Context.ConnectionId);
        }
        public Task TouchMove(double moveX, double moveY)
        {
            return RCDeviceHubContext.Clients.Client(ScreenCasterID).SendAsync("TouchMove", moveX, moveY, Context.ConnectionId);
        }
        public Task TouchUp()
        {
            return RCDeviceHubContext.Clients.Client(ScreenCasterID).SendAsync("TouchUp", Context.ConnectionId);
        }
    }
}
