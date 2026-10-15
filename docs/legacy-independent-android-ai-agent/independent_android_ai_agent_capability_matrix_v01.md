# Capability Matrix — Independent Android Personal AI Agent

**Version:** 0.1 — preliminary feasibility matrix  
**Date:** 2026-10-07  
**Scope:** Android + Web + AI + security + multimodal interface + Avatar/Persona

## Status legend
- **Core** — strong MVP candidate.
- **Strategic** — architecture should support it now.
- **Later** — feasible but not first-release priority.
- **Restricted** — possible only under significant platform/provider/policy constraints.
- **Avoid** — must not become a foundation.

## Executive conclusion

The agent should be built around a **Capability Registry + Policy/Risk Engine**, selecting the strongest available mechanism in this order:

1. Official Android API
2. Official app API/SDK
3. Android AppFunctions
4. Remote API / Remote MCP
5. Intent / App Links / Sharesheet
6. User-selected files/media
7. Web connector/browser
8. Narrow deterministic automation where policy permits

**AccessibilityService must not be the architectural foundation.** Google Play says ordinary apps cannot use it to autonomously initiate, plan and execute actions or decisions. citeturn0search2turn0search4

Android AppFunctions are particularly important: on Android 16+, apps can expose discrete capabilities that authorized agents can discover and execute. Google presents AppFunctions and Remote MCP as complementary parts of its Android intelligence architecture; Computer Control is described as an upcoming OEM-preloaded-assistant capability. citeturn0search11turn0search13

## Matrix

| # | Category | Capability | Mechanism | Access | Status / limitation | Priority |
|---:|---|---|---|---|---|---|
| 1 | System | Read Android/OS version | Build/OS APIs | None | Normal | **Core** |
| 2 | System | Read device model/manufacturer | Build APIs | None | Normal | **Core** |
| 3 | System | Read device capability profile | PackageManager + hardware APIs | None/limited | Normal | **Core** |
| 4 | System | Detect camera availability | CameraManager | None | Normal | **Core** |
| 5 | System | Detect microphone availability | AudioManager | None | Normal | **Core** |
| 6 | System | Detect network state | ConnectivityManager | None | Normal | **Core** |
| 7 | System | Detect battery/charging state | BatteryManager | None | Normal | **Core** |
| 8 | System | Detect available sensors | SensorManager | None | Normal | **Later** |
| 9 | System | Read screen size/density | Resources/WindowManager | None | Normal | **Core** |
| 10 | System | Read installed-app inventory | PackageManager | None/visibility restrictions | Policy/visibility rules | **Later** |
| 11 | System | Open another app | Intent/package launch | Usually none | Normal | **Core** |
| 12 | System | Open app-specific screen | Intent/deep link | Depends on target app | Normal | **Core** |
| 13 | System | Become default Assistant | RoleManager / VoiceInteractionService | User role selection | Strategic role | **Later** |
| 14 | System | Become default Browser | RoleManager | User role selection | Possible | **Later** |
| 15 | System | Act as Home/Launcher | ROLE_HOME | User role selection | Possible, major scope | **Later** |
| 16 | Permissions | Request runtime permission | Android runtime permissions | User consent | Normal | **Core** |
| 17 | Permissions | Explain missing permission | Permission state + UI | None | Normal | **Core** |
| 18 | Permissions | Detect special app access | Special App Access APIs | Varies | Special access | **Core** |
| 19 | Permissions | Draw over other apps | SYSTEM_ALERT_WINDOW | User grants Special App Access | Sensitive/policy implications | **Later** |
| 20 | Permissions | Schedule exact alarms | SCHEDULE_EXACT_ALARM | User/system rules | Special access | **Later** |
| 21 | Permissions | Broad storage access | MANAGE_EXTERNAL_STORAGE | Special access | Highly restricted | **Avoid** |
| 22 | Notifications | Receive own notifications | NotificationManager | POST_NOTIFICATIONS on Android 13+ | Normal | **Core** |
| 23 | Notifications | Read other-app notifications | NotificationListenerService | User grants notification access | Supported; privacy-sensitive | **Core** |
| 24 | Notifications | Detect notification arrival/removal | NotificationListenerService | Notification access | Supported | **Core** |
| 25 | Notifications | AI notification summary | Notification listener + local processing | Notification access | Privacy-sensitive | **Core** |
| 26 | Notifications | Trigger notification action | Notification actions/PendingIntent | Depends on source | Supported where exposed | **Later** |
| 27 | Notifications | Post AI task progress | NotificationManager | POST_NOTIFICATIONS | Normal | **Core** |
| 28 | Notifications | General full-screen agent alerts | Full-screen intent | Special permission/policy | Restricted to narrow use cases | **Avoid** |
| 29 | Files | Open user-selected file | Storage Access Framework | User picker | No broad storage permission | **Core** |
| 30 | Files | Save file where user chooses | ACTION_CREATE_DOCUMENT | User picker | Normal | **Core** |
| 31 | Files | Read selected directory | ACTION_OPEN_DOCUMENT_TREE | User grants URI access | Normal | **Core** |
| 32 | Files | Share file to another app | Intent + content URI/FileProvider | User/URI grant | Supported | **Core** |
| 33 | Files | Read all user files automatically | MANAGE_EXTERNAL_STORAGE | Special access | Highly restricted | **Avoid** |
| 34 | Media | Select photo for AI | Android Photo Picker | User selection | Preferred privacy path | **Core** |
| 35 | Media | Select video for AI | Android Photo Picker | User selection | Preferred privacy path | **Core** |
| 36 | Media | Selected photos persistent access | Photo Picker/selected access | User-controlled | Android 14+ privacy model | **Core** |
| 37 | Media | Read all photos | READ_MEDIA_IMAGES | Runtime permission/policy | Use only if picker insufficient | **Later** |
| 38 | Media | Read all videos | READ_MEDIA_VIDEO | Runtime permission/policy | Use only if picker insufficient | **Later** |
| 39 | Camera | Take photo | CameraX/Camera2 | CAMERA | Normal | **Core** |
| 40 | Camera | Live camera analysis | CameraX/Camera2 | CAMERA | Foreground feasible | **Core** |
| 41 | Camera | OCR from camera | On-device OCR/ML | CAMERA | Feasible | **Core** |
| 42 | Camera | Object/scene understanding | Local/cloud vision | CAMERA + network if cloud | Feasible | **Core** |
| 43 | Camera | Visual troubleshooting | Camera + vision + web | CAMERA/network | Feasible | **Core** |
| 44 | Camera | Background camera operation | Foreground service | CAMERA + FGS requirements | Strong restrictions | **Avoid** |
| 45 | Audio | Speech input | Microphone + speech recognition | RECORD_AUDIO | Normal | **Core** |
| 46 | Audio | Local speech-to-text | On-device speech model | Microphone | Hardware/model dependent | **Core** |
| 47 | Audio | Cloud speech-to-text | API/SDK | Microphone + network | Feasible | **Core** |
| 48 | Audio | Text-to-speech | Android TTS | None | Normal | **Core** |
| 49 | Audio | Streaming voice conversation | Audio APIs + FGS where appropriate | RECORD_AUDIO | Foreground/background rules | **Core** |
| 50 | Audio | Continuous background listening | FGS/VoiceInteractionService | RECORD_AUDIO + role/rules | Restricted | **Later** |
| 51 | Video | AI video conversation | Camera + microphone + multimodal pipeline | CAMERA + RECORD_AUDIO | Foreground UX feasible | **Core** |
| 52 | Video | Avatar lip-sync | Local animation/rendering | None | Normal | **Core** |
| 53 | Video | Avatar listening/thinking states | UI state machine | None | Normal | **Core** |
| 54 | Video | Avatar task-progress states | Agent event stream | None | Normal | **Core** |
| 55 | Video | Customizable avatar | Local 2D/3D asset system | None | Normal | **Core** |
| 56 | Video | Download avatar skins | CDN/content system | Network | Normal | **Core** |
| 57 | Video | Community avatar skins | Content platform | Network/account | Moderation/IP required | **Later** |
| 58 | Screen | User-selected screen capture | MediaProjection | Explicit system consent | Supported | **Core** |
| 59 | Screen | AI screen analysis | MediaProjection + OCR/vision | User consent | Feasible | **Core** |
| 60 | Screen | Continuous screen understanding | MediaProjection | Ongoing user-controlled session | Privacy/battery sensitive | **Later** |
| 61 | Screen | Screen recording | MediaProjection | Explicit consent | Supported with rules | **Later** |
| 62 | Screen | Secret screen capture | Not legitimate | None | Not acceptable | **Avoid** |
| 63 | Clipboard | Use user-shared clipboard | ClipboardManager | User/system restrictions | Limited by Android privacy | **Later** |
| 64 | Clipboard | Persist sensitive clipboard | Local storage | N/A | High privacy risk | **Avoid** |
| 65 | Contacts | Pick contact for task | Android Contact Picker | User selection | Preferred | **Core** |
| 66 | Contacts | Read broad contacts | Contacts Provider | READ_CONTACTS | Policy-sensitive | **Later** |
| 67 | Calendar | Read calendar | Calendar Provider | READ_CALENDAR | Permission/sensitive | **Later** |
| 68 | Calendar | Create calendar event | Calendar Provider / intent | WRITE_CALENDAR or user flow | Feasible | **Core** |
| 69 | Calendar | Prepare event without saving | Agent draft | None | Normal | **Core** |
| 70 | Location | Approximate location | Fused Location Provider | COARSE_LOCATION | Runtime permission | **Core** |
| 71 | Location | Precise location | Fused Location Provider | FINE_LOCATION | Runtime permission | **Core** |
| 72 | Location | Background location | Location APIs | ACCESS_BACKGROUND_LOCATION | Highly sensitive | **Later** |
| 73 | Location | Geofencing | Geofencing API | Location permissions | Policy evolving; recheck | **Later** |
| 74 | Location | Contextual location answers | Location + agent | User permission | Feasible | **Core** |
| 75 | Connectivity | Read network state | ConnectivityManager | None | Normal | **Core** |
| 76 | Connectivity | Nearby Wi-Fi devices | NEARBY_WIFI_DEVICES | Runtime permission | Supported | **Later** |
| 77 | Connectivity | Bluetooth discovery | Bluetooth APIs | Nearby devices permissions | Supported | **Later** |
| 78 | Connectivity | Known Bluetooth device interaction | Bluetooth APIs | Nearby devices permissions | Supported | **Later** |
| 79 | Connectivity | NFC interaction | NFC APIs | NFC hardware | Supported | **Later** |
| 80 | Web | Web search | Search API/web connector | Network | Core | **Core** |
| 81 | Web | Fetch public web pages | HTTP/web connector | Network | Core | **Core** |
| 82 | Web | Parse structured web data | HTTP/parser | Network | Depends on site/API terms | **Core** |
| 83 | Web | Browser deep link | Intent/App Links | Usually none | Supported | **Core** |
| 84 | Web | Open browser page | ACTION_VIEW | None | Supported | **Core** |
| 85 | Web | Default browser role | ROLE_BROWSER | User role selection | Possible | **Later** |
| 86 | Web | Automate arbitrary website UI | Browser automation | Varies | Fragile/terms/security concerns | **Later** |
| 87 | Web | Authenticated website automation | Browser/session automation | User auth | High security risk | **Later** |
| 88 | Apps | Launch installed app | PackageManager/Intent | Usually none | Visibility rules | **Core** |
| 89 | Apps | Send text to another app | ACTION_SEND/Sharesheet | User choice | Supported | **Core** |
| 90 | Apps | Share image/PDF | ACTION_SEND + content URI | User/URI grant | Supported | **Core** |
| 91 | Apps | Open specific feature | Intent/deep link | Target app must expose it | Best integration path | **Core** |
| 92 | Apps | Discover AppFunctions | Android AppFunctions | EXECUTE_APP_FUNCTIONS / supported environment | Android 16+; evolving | **Strategic** |
| 93 | Apps | Execute AppFunction | Android AppFunctions | Authorized caller | Android 16+; evolving | **Strategic** |
| 94 | Apps | Use Remote MCP service | Remote MCP | Network/auth | Complementary architecture | **Strategic** |
| 95 | Apps | Invoke undocumented private API | Private API/reverse engineering | Varies | Fragile/avoid | **Avoid** |
| 96 | Apps | Autonomous Accessibility control | AccessibilityService | User enable + declaration | Prohibited for ordinary apps | **Avoid** |
| 97 | Apps | Deterministic narrow accessibility automation | AccessibilityService | User enable + disclosure/declaration | Narrow policy use only | **Restricted** |
| 98 | Assistant | Offer as Android Assistant | VoiceInteractionService/ROLE_ASSISTANT | User selects default | Strategic | **Later** |
| 99 | Assistant | Receive assistant invocation | VoiceInteractionService | Assistant role | Supported role path | **Later** |
| 100 | Calls | Initiate phone call | ACTION_DIAL/ACTION_CALL | CALL_PHONE for direct call | User-controlled | **Later** |
| 101 | Calls | Read call log | Call Log Provider | Default Phone/Assistant + policy | Restricted | **Restricted** |
| 102 | Calls | Send SMS | SMS APIs/role | Default SMS/Assistant + policy | Restricted | **Restricted** |
| 103 | Calls | Read SMS | SMS provider/role | Default SMS/Assistant + policy | Restricted | **Restricted** |
| 104 | Calls | Call screening | ROLE_CALL_SCREENING | User role | Specific role | **Later** |
| 105 | Calls | Call redirection | ROLE_CALL_REDIRECTION | User role | Specific role | **Later** |
| 106 | Payments | Open banking/payment app | Intent/deep link | Target app | Supported if exposed | **Core** |
| 107 | Payments | Prepare payment | Connector/app API | Account auth | Provider-dependent | **Later** |
| 108 | Payments | Execute transfer autonomously | Bank API/connector | Strong auth | High-risk; confirmation required | **Restricted** |
| 109 | Payments | Read banking data | Official API/connector | OAuth/user consent | Provider-dependent | **Later** |
| 110 | Identity | Secure credential storage | Android Keystore | None/user auth as configured | Core primitive | **Core** |
| 111 | Identity | Biometric confirmation | BiometricPrompt | User biometric | Supported | **Core** |
| 112 | Identity | Passkey/WebAuthn | Credential Manager | User interaction | Supported | **Later** |
| 113 | Security | Audit agent actions | Local event log | None | Core requirement | **Core** |
| 114 | Security | Risk-based confirmation | Policy engine + UI | User confirmation | Core requirement | **Core** |
| 115 | Security | Verify action result | Tool response + postconditions | Depends on tool | Core requirement | **Core** |
| 116 | Security | Explain permission need | Permission broker/UI | None | Core UX | **Core** |
| 117 | Security | Local-only sensitive processing | On-device models/OCR | None | Hardware dependent | **Core** |
| 118 | Security | Redact before cloud LLM | Privacy Router/DLP | None | Core architecture | **Core** |
| 119 | AI | Local LLM | On-device inference | None | Hardware/model dependent | **Core** |
| 120 | AI | Cloud LLM | Provider API | Network/credential | Feasible | **Core** |
| 121 | AI | Vision model | Local/cloud vision | Image/camera | Feasible | **Core** |
| 122 | AI | Speech model | Local/cloud STT/TTS | Audio | Feasible | **Core** |
| 123 | AI | Model Router | Agent orchestration | None | Core architecture | **Core** |
| 124 | AI | Task decomposition | Agent planner | None | Core architecture | **Core** |
| 125 | AI | Tool selection | Capability registry | None | Core architecture | **Core** |
| 126 | AI | Capability self-awareness | Registry + runtime state | None | Core architecture | **Core** |
| 127 | AI | User preference memory | Encrypted local memory | User consent | Privacy-sensitive | **Later** |
| 128 | AI | Cloud long-term memory | Encrypted backend | Account/network | Needs explicit privacy model | **Later** |
| 129 | AI | Offline degraded mode | Local models + cached tools | None | Hardware dependent | **Core** |
| 130 | AI | Network-aware model selection | Router + connectivity | None | Core | **Core** |
| 131 | UI | Compact avatar launcher | Widget/launcher/approved UI mechanism | Depends on implementation | Normal if no prohibited overlay | **Core** |
| 132 | UI | Tap avatar to expand | Activity/Compose animation | None | Normal | **Core** |
| 133 | UI | Animated agent states | Compose/animation | None | Normal | **Core** |
| 134 | UI | Avatar voice personality | TTS + persona | None | Normal | **Core** |
| 135 | UI | Avatar skin catalog | Backend/CDN/store | Network/account | Moderation/IP | **Later** |
| 136 | UI | Dynamic agent-generated UI | A2UI/Compose renderer | Supported evolving architecture | Strategic/evolving | **Later** |
| 137 | UI | Tablet/large-screen support | Responsive Compose | None | Normal | **Core** |
| 138 | UI | Wearable/companion UI | Device-specific APIs | Device-dependent | Later | **Later** |
| 139 | Background | Scheduled agent task | WorkManager/alarms | None/special access by precision | Normal with limits | **Core** |
| 140 | Background | Long-running task | WorkManager/FGS | Depends on task | FGS restrictions | **Core** |
| 141 | Background | Background microphone task | FGS/VoiceInteractionService | RECORD_AUDIO + rules | Restricted | **Later** |
| 142 | Background | Background camera task | FGS camera | CAMERA + FGS | Strong restrictions | **Avoid** |
| 143 | Background | Persistent arbitrary background execution | Undocumented/abusive service | N/A | Not viable | **Avoid** |
| 144 | Enterprise | Managed-device integration | DevicePolicyManager | Device/profile owner | Enterprise only | **Later** |
| 145 | Enterprise | Enterprise distribution | Managed Play/private distribution | Admin | Useful alternative distribution | **Later** |
## Architectural conclusions

### Capability Registry

Every capability should have machine-readable metadata:

```text
capability_id
availability
required_permissions
special_access
risk_level
data_sensitivity
execution_method
confirmation_required
verification_method
fallbacks
```

This prevents the model from guessing what the phone can do.

### Notifications

NotificationListenerService can receive callbacks when notifications are posted or removed, making the proposed AI Inbox realistic. It is privacy-sensitive and should have explicit data handling rules. citeturn2search3

### Files and media

Use the Storage Access Framework and Photo Picker first. SAF lets the user select files/directories without broad storage permission; Android 14 also introduced selected-photo access. citeturn2search11turn2search8

### Screen context

MediaProjection provides a user-consented route for screen capture, making an explicit AI screen-context feature realistic. citeturn1search15

### Voice/video

Camera and microphone are realistic for an interactive foreground experience. Modern Android requires explicit foreground-service types/permissions when FGS is used, and background starts/access are restricted. citeturn1search1turn1search7turn1search9

### SMS/call log

These should not be early dependencies. Google Play restricts sensitive SMS/Call Log access to qualifying default SMS/Phone/Assistant handlers and approved core uses. citeturn0search1turn0search4

### Contacts

Prefer user-selected contacts via the Contact Picker rather than broad address-book access. Google Play's current policy roadmap explicitly promotes this approach. citeturn0search3

### Deep links

Deep links/App Links are a strong integration primitive for opening specific content in other apps/services. Verified App Links create a trusted website-app association; Android 15 added Dynamic App Links. citeturn1search0turn1search4

### Avatar architecture

The avatar should be an independent **Persona & Avatar Layer**, not part of the Agent Core:

```text
Agent Core
 ├─ Capability System
 ├─ Model Router
 ├─ Risk Engine
 └─ Action Executor
          |
          v
Persona / Avatar Layer
 ├─ name
 ├─ skin
 ├─ animation
 ├─ state
 ├─ voice
 └─ personality
```

This allows skins, names and visual behavior to evolve without changing the AI runtime.

## Recommended MVP

1. Text
2. Voice input/output
3. Compact animated avatar → expanded workspace
4. Model Router
5. Capability Registry
6. Risk/confirmation engine
7. Audit log
8. Web search/retrieval
9. User-selected documents
10. Photo/video selection
11. Camera + OCR/vision
12. Explicit screen-context capture
13. Notifications / AI Inbox
14. Intents / Deep Links / Sharesheet
15. Local encrypted storage
16. Privacy Router
17. Device capability awareness
18. Offline/degraded mode
19. Task planning + result verification

## Architect now, implement later

- AppFunctions
- Remote MCP
- Android Assistant role
- Calendar
- Contacts
- Bluetooth/NFC
- location context
- official service connectors
- advanced browser automation
- long-term memory
- community avatar skins
- A2UI
- enterprise/device-management integrations

## Do not make foundational dependencies

- root access
- undocumented/private APIs
- broad storage access
- autonomous Accessibility automation
- arbitrary background camera
- unrestricted background microphone
- autonomous banking/payment execution
- silent clipboard/screen collection
- assumption that every third-party app is controllable

## Security execution pipeline

```text
REQUEST
  ↓
CAPABILITY CHECK
  ↓
PERMISSION CHECK
  ↓
RISK CLASSIFICATION
  ↓
USER CONFIRMATION if required
  ↓
EXECUTION
  ↓
RESULT VERIFICATION
  ↓
AUDIT RECORD
```

The agent must never claim success merely because the model predicted success.

## Policy dates to track

Google Play currently lists **2027-01-27** for several sensitive-information policy changes, including Contacts Permissions and SMS/Call Log changes, plus changes related to foreground-service/full-screen-intent requirements. These policies must be rechecked immediately before implementation and release. citeturn0search3

## Primary sources

- Android AppFunctions: https://developer.android.com/ai/appfunctions
- Android intelligence system: https://developer.android.com/ai/intelligence-system
- Accessibility API policy: https://support.google.com/googleplay/android-developer/answer/10964491
- Sensitive permissions policy: https://support.google.com/googleplay/android-developer/answer/16558241
- SMS/Call Log policy: https://support.google.com/googleplay/android-developer/answer/10208820
- Special permissions: https://developer.android.com/training/permissions/requesting-special
- NotificationListenerService: https://developer.android.com/reference/android/service/notification/NotificationListenerService
- App Links: https://developer.android.com/training/app-links/about
- Storage Access Framework: https://developer.android.com/training/data-storage/shared/documents-files
- Selected photos: https://developer.android.com/about/versions/14/changes/partial-photo-video-access
- Foreground services: https://developer.android.com/develop/background-work/services/fgs
- RoleManager: https://developer.android.com/reference/android/app/role/RoleManager
- MediaProjection: https://developer.android.com/reference/android/media/projection/MediaProjectionConfig

## Final assessment

The project is technically viable if it is designed as a **capability-aware Android AI runtime**, not as an unrestricted controller of other applications.

The strongest preliminary combination is:

**Agent Core + Capability Registry + Model Router + Privacy Router + Risk Engine + official Android integrations + AppFunctions/Remote MCP + multimodal interface + distinctive Avatar/Persona Layer.**

This gives the project a path that does not depend on privileged or prohibited control over Android.
