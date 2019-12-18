﻿using System;
using System.Collections.Generic;
using System.Linq;
using ResultDotNet;
using SoundSwitch.Audio.Manager;
using SoundSwitch.Audio.Manager.Interop.Enum;
using SoundSwitch.Framework.Audio.Device;
using SoundSwitch.Framework.Configuration;
using SoundSwitch.Localization;

namespace SoundSwitch.Framework.Profile
{
    public class ProfileManager
    {
        private readonly Dictionary<HotKeys, ProfileSetting> _profileByHotkey;
        private readonly Dictionary<string, ProfileSetting> _profileByApplication;
        private readonly ForegroundProcessChanged _foregroundProcessChanged;
        private readonly AudioSwitcher _audioSwitcher;

        public ProfileManager(ForegroundProcessChanged foregroundProcessChanged, AudioSwitcher audioSwitcher)
        {
            _foregroundProcessChanged = foregroundProcessChanged;
            _audioSwitcher = audioSwitcher;
            _profileByApplication =
                AppConfigs.Configuration.ProfileSettings
                    .Where((setting) => setting.ApplicationPath != null)
                    .ToDictionary(setting => setting.ApplicationPath.ToLower());
            _profileByHotkey = AppConfigs.Configuration.ProfileSettings
                .Where((setting) => setting.HotKeys != null)
                .ToDictionary(setting => setting.HotKeys);

            RegisterEvents();
        }

        private void RegisterEvents()
        {
            _foregroundProcessChanged.Event += (sender, @event) =>
            {
                _profileByApplication.TryGetValue(@event.ProcessName.ToLower(), out var profile);
                if (profile == null)
                    return;
                SwitchAudio(profile, @event.ProcessId);
            };

            WindowsAPIAdapter.HotKeyPressed += (sender, args) =>
            {
                _profileByHotkey.TryGetValue(args.HotKeys, out var profile);
                if (profile == null)
                    return;
                SwitchAudio(profile);
            };
        }

        private void SwitchAudio(ProfileSetting profile, uint processId = 0)
        {
            foreach (var device in new[] {profile.Playback, profile.Recording}.Where((info) => info != null))
            {
                if (processId != 0)
                {
                    _audioSwitcher.SwitchProcessTo(device.Id, ERole.ERole_enum_count, (EDataFlow) device.Type,
                        processId);
                    
                }
                //Always switch the default device.
                //Easy way to be sure a notification will be send.
                //And to be consistent with the default audio device.
                _audioSwitcher.SwitchTo(device.Id, ERole.ERole_enum_count);
            }
        }

        /// <summary>
        /// Add a profile to the system
        /// </summary>
        /// <param name="profile"></param>
        /// <returns></returns>
        public Result<ProfileSetting, string> AddProfile(ProfileSetting profile)
        {
            if (profile.ApplicationPath == null && profile.HotKeys == null)
            {
                return Result<ProfileSetting, string>.NewError(SettingsStrings.profile_error_needHKOrPath);
            }

            if (profile.Recording == null && profile.Playback == null)
            {
                return Result<ProfileSetting, string>.NewError(SettingsStrings.profile_error_needPlaybackOrRecording);
            }

            if (profile.HotKeys != null && _profileByHotkey.ContainsKey(profile.HotKeys))
            {
                return Result<ProfileSetting, string>.NewError(string.Format(SettingsStrings.profile_error_hotkey,
                    profile.HotKeys));
            }

            if (profile.ApplicationPath != null && _profileByApplication.ContainsKey(profile.ApplicationPath))
            {
                return Result<ProfileSetting, string>.NewError(string.Format(SettingsStrings.profile_error_application,
                    profile.ApplicationPath));
            }

            if (AppConfigs.Configuration.ProfileSettings.Contains(profile))
            {
                return Result<ProfileSetting, string>.NewError(string.Format(SettingsStrings.profile_error_name,
                    profile.ProfileName));
            }

            if (profile.HotKeys != null && !WindowsAPIAdapter.RegisterHotKey(profile.HotKeys))
            {
                return Result<ProfileSetting, string>.NewError(string.Format(SettingsStrings.profile_error_hotkey,
                    profile.HotKeys));
            }


            if (profile.ApplicationPath != null)
                _profileByApplication.Add(profile.ApplicationPath.ToLower(), profile);
            if (profile.HotKeys != null)
                _profileByHotkey.Add(profile.HotKeys, profile);

            AppConfigs.Configuration.ProfileSettings.Add(profile);
            AppConfigs.Configuration.Save();

            return Result.Ok<ProfileSetting, string>(profile);
        }
    }
}