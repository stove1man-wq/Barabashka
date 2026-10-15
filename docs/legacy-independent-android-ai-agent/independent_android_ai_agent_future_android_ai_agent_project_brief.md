# Future Project --- Independent Android Personal AI Agent

## Project Brief + Preliminary Architecture Draft

**Status:** Preliminary concept / pre-development analysis\
**Date:** 2026-10-07\
**Implementation status:** Not started\
**Important:** This is a new project from zero. The previous "Universal
AI" project is used only as a source of experience and lessons learned;
its codebase is not being reused.

------------------------------------------------------------------------

# 1. Project idea

The project is an independent **Personal AI Agent / AI Agent Runtime for
Android**.

The goal is not to build another ordinary AI chat application. The goal
is to create an independent AI layer that can become a user's interface
to:

-   information;
-   the web;
-   Android capabilities;
-   applications;
-   documents and files;
-   notifications;
-   voice, camera and video context;
-   external APIs and services;
-   local and cloud AI models.

The agent should be able to understand a user's goal, determine what
capabilities are available, select the appropriate AI model and tools,
prepare or execute actions, and clearly explain what it can and cannot
do.

The project should not depend on one particular AI model or one
particular application ecosystem.

------------------------------------------------------------------------

# 2. Main product vision

A possible high-level formulation:

> **A personal AI environment that understands the user's intent and,
> within explicitly granted capabilities, can work with information,
> applications, web services and the Android device itself.**

The product should feel less like a chatbot and more like an **AI
operating layer / personal agent runtime**.

The central principle:

**The user gives an intention; the agent determines the safest and most
appropriate way to accomplish it.**

Example:

> "Find the cheapest suitable flight, compare the options and prepare
> the best one for booking."

The agent should be able to: 1. understand the goal; 2. search available
sources; 3. compare results; 4. explain the recommendation; 5. prepare
the booking; 6. stop before a consequential action; 7. request explicit
confirmation before purchase.

------------------------------------------------------------------------

# 3. Why build it independently

Large applications and service providers may not want unrestricted
third-party AI agents controlling their applications.

Therefore the architecture should not assume:

-   direct control of every popular app;
-   unrestricted accessibility automation;
-   unrestricted access to SMS/call logs;
-   private undocumented APIs;
-   root access;
-   cooperation from every service provider.

Instead, the agent should use a **layered capability strategy**.

Preferred order:

1.  Official Android APIs
2.  Official application APIs / SDKs
3.  Android AppFunctions where available
4.  Remote APIs / connectors
5.  Android Intents / Deep Links / App Links
6.  Android Sharesheet and controlled data exchange
7.  Web/browser interaction
8.  UI automation only where technically and legally/policy permitted
9.  Accessibility automation must NOT be the foundation of the product

The product must remain useful even when a particular integration is
unavailable.

------------------------------------------------------------------------

# 4. Core design principles

## 4.1 Capability-based architecture

The agent does not receive unrestricted access to the device.

Instead, it sees a set of explicit capabilities.

Conceptually:

``` text
Agent
  |
  +-- Capability Manager
        |
        +-- Camera
        +-- Microphone
        +-- Notifications
        +-- Files
        +-- Location
        +-- Contacts
        +-- Calendar
        +-- Web
        +-- AppFunctions
        +-- Intents
        +-- Connectors
        +-- Screen/context
        +-- Other approved capabilities
```

The agent should know which capabilities are:

-   available;
-   unavailable;
-   temporarily unavailable;
-   permission-protected;
-   requiring user confirmation;
-   restricted by Android/Google Play;
-   requiring a partner API;
-   potentially available only on particular devices/OEMs.

------------------------------------------------------------------------

# 5. Risk model

Actions should be divided into at least three levels.

## Level 1 --- Observation

Examples:

-   read notifications;
-   search the web;
-   inspect a document;
-   summarize information;
-   analyze a photo;
-   compare products.

Normally no confirmation is required once the relevant permission has
already been granted.

## Level 2 --- Preparation

Examples:

-   draft a message;
-   prepare an email;
-   prepare a purchase;
-   fill out a form without submitting it;
-   prepare a calendar event;
-   build a travel itinerary.

The agent may perform preparation but should not perform the final
consequential action automatically.

## Level 3 --- Action

Examples:

-   send a message;
-   make a purchase;
-   transfer money;
-   delete important data;
-   change security settings;
-   publish content;
-   perform another irreversible or consequential operation.

These require explicit confirmation according to the project's security
policy.

------------------------------------------------------------------------

# 6. Security architecture

Security should be part of the architecture from the beginning rather
than added later.

Preliminary security components:

``` text
                    +------------------+
                    |   User / Identity|
                    +--------+---------+
                             |
                             v
+-------------+      +--------------------+
| AI Models   | <--> |   Agent Core       |
+-------------+      +---------+----------+
                              |
                              v
                    +--------------------+
                    | Policy / Risk      |
                    | Engine              |
                    +---------+----------+
                              |
                    +---------v----------+
                    | Capability Manager |
                    | / Permission Broker|
                    +---------+----------+
                              |
                    +---------v----------+
                    | Action Executor     |
                    +---------+----------+
                              |
                    +---------v----------+
                    | Android / Web / API|
                    +--------------------+

                    +--------------------+
                    | Audit Log           |
                    +--------------------+
```

Important concepts:

-   explicit permissions;
-   capability isolation;
-   user consent;
-   action confirmation;
-   audit log;
-   policy engine;
-   risk classification;
-   identity;
-   data boundaries;
-   secure storage;
-   minimal data transfer;
-   clear explanation of why an action was requested.

------------------------------------------------------------------------

# 7. Privacy Router

A dedicated privacy/data-routing layer is desirable.

The system should decide:

> What information needs to stay on the device, and what information can
> be sent to an external AI service?

Example:

``` text
Camera
   |
   v
Local OCR / local vision
   |
   +---- sensitive raw image stays local
   |
   v
Minimal extracted information
   |
   v
Cloud LLM for reasoning
```

Possible routing criteria:

-   sensitivity;
-   user policy;
-   task complexity;
-   model availability;
-   latency;
-   cost;
-   local hardware capability;
-   network availability.

This creates a path toward a hybrid architecture:

**local AI + cloud AI + specialized models.**

------------------------------------------------------------------------

# 8. Model architecture

The system should not be hard-coded around one model.

Preliminary model layer:

``` text
                 +----------------------+
                 |    Model Router      |
                 +----------+-----------+
                            |
          +-----------------+------------------+
          |                 |                  |
          v                 v                  v
    Local LLM          Cloud LLM         Specialized
    / Small Model      / Large Model     Vision/OCR/etc.
```

The router may select a model according to:

-   task complexity;
-   privacy;
-   latency;
-   available hardware;
-   context size;
-   vision/audio requirements;
-   cost;
-   network availability.

The architecture should allow models to be replaced without rewriting
the agent core.

------------------------------------------------------------------------

# 9. Agent Core

The central reasoning/orchestration subsystem is expected to contain:

``` text
User Intent
     |
     v
Intent Understanding
     |
     v
Task Decomposition
     |
     v
Planning
     |
     v
Capability Selection
     |
     v
Model / Tool Selection
     |
     v
Execution Plan
     |
     v
Risk Check
     |
     +----> Confirmation required?
     |             |
     |             +-- yes --> User confirmation
     |             |
     |             +-- no
     v
Action Execution
     |
     v
Result Verification
     |
     v
Explanation / Response
```

The agent should be able to stop when a required capability is
unavailable instead of pretending that the action was completed.

------------------------------------------------------------------------

# 10. Android integration layer

The Android integration layer should be modular.

Preliminary structure:

``` text
Android Adapter
|
+-- Permissions
+-- Notifications
+-- Camera
+-- Microphone
+-- Location
+-- Files / Media
+-- Contacts
+-- Calendar
+-- Intents
+-- Deep Links / App Links
+-- Sharesheet
+-- AppFunctions
+-- Screen / Context
+-- Sensors
+-- Browser / Web
+-- Other approved Android services
```

Each integration should be independently enabled or disabled.

This is important because Android capabilities and Google Play policies
vary significantly between functions.

------------------------------------------------------------------------

# 11. Web and external services

The agent should have a separate connector layer.

``` text
                +----------------+
                | Connector Core |
                +-------+--------+
                        |
       +----------------+----------------+
       |                |                |
       v                v                v
     REST/API         Web           Remote MCP
       |                |                |
       +----------------+----------------+
                        |
                        v
                  Agent Runtime
```

A connector should expose a controlled set of functions rather than
unrestricted credentials.

Possible future connectors:

-   search;
-   travel;
-   shopping;
-   email;
-   calendar;
-   cloud storage;
-   productivity tools;
-   financial services where officially supported;
-   messaging services where officially supported.

------------------------------------------------------------------------

# 12. Unified AI Inbox

One potentially strong product feature is a unified AI view of events.

For example:

``` text
Notifications
Messages
Calendar
Orders
Reminders
Email
System events
       |
       v
   AI Inbox
       |
       v
Prioritized / summarized events
```

The agent could answer:

> "What happened while I was away?"

and produce a prioritized summary.

This feature must be designed with strong privacy controls because
notifications and messages may contain highly sensitive information.

------------------------------------------------------------------------

# 13. Multimodal interface

The interface should not be limited to text.

Possible context sources:

``` text
TextInput
VoiceInput
CameraInput
VideoInput
ScreenContext
DocumentInput
NotificationContext
WebContext
DeviceContext
```

These should be treated as independent context providers.

## Video mode

The video module should not simply imitate a conventional video call.

Potential concept:

**real-time visual AI assistant**

The user can show:

-   a document;
-   device hardware;
-   a product;
-   a road sign;
-   a room;
-   a technical problem;
-   an object;
-   a screen.

The AI can combine:

-   live video;
-   audio;
-   conversation;
-   OCR;
-   visual recognition;
-   web search;
-   device context.

This could become one of the product's major differentiators.

------------------------------------------------------------------------

# 14. UI architecture

The interface should be designed around the agent's capabilities rather
than around a fixed set of screens.

Possible conceptual structure:

``` text
+--------------------------------------+
|           AI Agent Interface         |
+--------------------------------------+
| Chat / Voice / Video                 |
|                                      |
| Current task                         |
| Active capabilities                  |
| Suggested actions                    |
| Confirmation requests                |
| Results                              |
+--------------------------------------+
| Context / History / AI Inbox         |
+--------------------------------------+
```

The UI should clearly distinguish:

-   information;
-   recommendation;
-   preparation;
-   requested confirmation;
-   completed action;
-   failed action;
-   unavailable capability.

------------------------------------------------------------------------

# 15. Capability awareness

A fundamental property of the agent:

> The AI must know what it can and cannot do.

For example:

``` text
User:
"Send this message to John."

Agent:
"I can prepare the message, but sending it requires
messaging access / an available integration.
No message has been sent."
```

The system must never imply successful execution when execution did not
occur.

This principle should be enforced technically, not only through
prompting.

------------------------------------------------------------------------

# 16. Device / hardware awareness

The agent should be aware of device capabilities.

Examples:

-   microphone available/unavailable;
-   camera available/unavailable;
-   local AI acceleration;
-   available memory;
-   network status;
-   battery constraints;
-   sensor availability;
-   permissions;
-   OS version;
-   supported Android APIs.

This allows the system to select realistic execution paths.

------------------------------------------------------------------------

# 17. Offline / degraded operation

The architecture should support graceful degradation.

Possible states:

``` text
Full connectivity
      |
      v
Cloud + Local AI

Limited connectivity
      |
      v
Local AI + cached data

No network
      |
      v
Local capabilities only
```

The user should see what functionality remains available.

------------------------------------------------------------------------

# 18. Preliminary component map

A first architectural decomposition:

``` text
                           USER
                            |
                            v
                 +---------------------+
                 | Interaction Layer   |
                 | Text / Voice / Video|
                 +----------+----------+
                            |
                            v
                 +---------------------+
                 | Agent Core          |
                 | Intent / Planning   |
                 | Reasoning           |
                 | Memory              |
                 +----------+----------+
                            |
              +-------------+-------------+
              |                           |
              v                           v
     +-------------------+       +-------------------+
     | Model Router      |       | Capability Manager|
     | Local / Cloud /   |       | Permissions       |
     | Specialized       |       | Policies          |
     +-------------------+       +---------+---------+
                                           |
                                           v
                                  +-------------------+
                                  | Risk / Policy     |
                                  | Engine            |
                                  +---------+---------+
                                            |
                                            v
                                  +-------------------+
                                  | Action Executor   |
                                  +---------+---------+
                                            |
                    +-----------------------+----------------------+
                    |                       |                      |
                    v                       v                      v
              Android APIs           AppFunctions          Web / APIs
                    |                       |                      |
                    +-----------------------+----------------------+
                                            |
                                            v
                                  +-------------------+
                                  | Result Verification|
                                  +---------+---------+
                                            |
                                            v
                                  +-------------------+
                                  | Audit Log         |
                                  +-------------------+
```

Cross-cutting services:

-   Identity
-   Secure storage
-   Privacy Router
-   Telemetry / diagnostics
-   Configuration
-   Model registry
-   Capability registry
-   Versioning
-   Update mechanism

------------------------------------------------------------------------

# 19. What should NOT be a foundation

The project should not be architecturally dependent on:

-   root access;
-   unrestricted AccessibilityService automation;
-   undocumented private APIs;
-   one specific AI provider;
-   one specific cloud service;
-   one specific Android manufacturer;
-   permanent Internet connectivity;
-   direct control over every third-party application;
-   assumptions that Google Play will allow every desired automation
    mechanism.

These may create fragile architecture and deployment problems.

------------------------------------------------------------------------

# 20. Preliminary MVP philosophy

Do not begin by attempting to control the entire Android ecosystem.

The first MVP should prove the core loop:

``` text
User intention
      ↓
AI understands task
      ↓
AI determines available capabilities
      ↓
AI selects model/tool
      ↓
AI plans
      ↓
Risk check
      ↓
Execution / preparation
      ↓
Verification
      ↓
Transparent result
```

The MVP should therefore validate the **agent runtime**, not the number
of integrations.

A capability can be added later without redesigning the core.

------------------------------------------------------------------------

# 21. Proposed development stages

The exact stages are not fixed yet. A preliminary sequence:

### Stage 0 --- Research

-   Capability Matrix
-   Android/Google Play restrictions
-   API availability
-   security model
-   privacy model
-   feasibility
-   competitive analysis

### Stage 1 --- Core specification

-   terminology
-   contracts
-   capability model
-   task model
-   risk model
-   permission model
-   audit model

### Stage 2 --- Agent Runtime prototype

-   intent handling
-   planning
-   model router
-   tool abstraction
-   capability registry

### Stage 3 --- Android shell

-   permissions
-   device capabilities
-   basic Android integration
-   secure storage

### Stage 4 --- First real capabilities

-   web
-   notifications
-   files/documents
-   intents/deep links
-   selected official integrations

### Stage 5 --- Multimodal

-   voice
-   camera
-   OCR
-   visual reasoning
-   video assistant

### Stage 6 --- Security and trust

-   policy engine
-   risk engine
-   confirmation system
-   audit log
-   privacy router

### Stage 7 --- Expansion

-   AppFunctions
-   connectors
-   more services
-   offline/local AI
-   advanced automation where officially supported

These stages are only a preliminary planning skeleton and should be
revised after the Capability Matrix.

------------------------------------------------------------------------

# 22. Project rules established so far

1.  The new project is built **from zero**.
2.  The previous Universal AI project is used only for experience and
    lessons learned.
3.  Existing Universal AI code is not the foundation of this project.
4.  Architecture is designed before large-scale implementation.
5.  Feasibility is checked before committing to a feature.
6.  Android and Google Play limitations are treated as architectural
    constraints.
7.  Security and privacy are designed from the beginning.
8.  No assumption of root access.
9.  No dependence on unrestricted accessibility automation.
10. No dependence on a single AI model/provider.
11. The agent must know and communicate its actual capabilities.
12. Consequential actions require explicit confirmation.
13. Every important action should be verifiable and auditable.
14. The system should support modular capabilities.
15. Development should proceed in saved, clearly defined stages.

------------------------------------------------------------------------

# 23. Open questions --- deliberately not solved yet

The following should be decided after further analysis:

-   Exact MVP feature set
-   Android minimum version
-   Target Android versions
-   Whether the first release is Google Play oriented or
    sideload/enterprise oriented
-   Local model strategy
-   Cloud model providers
-   Memory architecture
-   Long-term user memory policy
-   Connector architecture
-   Exact security model
-   Credential/token storage
-   Multi-device synchronization
-   Monetization
-   Product name / trademark strategy
-   Patentability of any genuinely novel technical mechanisms
-   Exact video architecture
-   Exact UI architecture
-   Capability Matrix
-   Testing strategy
-   Release/update strategy

------------------------------------------------------------------------

# 24. Next analytical document

The next major document should be the:

## Android / Web / AI Capability Matrix

For each potential capability it should determine:

-   what the user wants to achieve;
-   Android mechanism;
-   required permission;
-   whether special access is required;
-   whether an official API exists;
-   whether AppFunctions can be used;
-   whether an external API is required;
-   whether browser/web access is possible;
-   Google Play policy status;
-   technical limitations;
-   privacy/security implications;
-   feasibility;
-   recommended implementation layer;
-   MVP / later / avoid classification;
-   possible alternative if the primary mechanism is unavailable.

The Capability Matrix should be based on current Android and Google Play
documentation and should become the main input for the final
architecture and MVP definition.

------------------------------------------------------------------------

# 25. Current architectural thesis

The strongest preliminary thesis is:

> **Build an independent, capability-aware AI Agent Runtime for Android
> rather than a traditional AI chat application.**

Its value should come from the combination of:

**AI reasoning + model routing + capabilities + permissions + security +
multimodal context + Android/Web integration + transparent execution.**

The key competitive advantage should not be "the smartest model."

It should be:

> **the most useful, trustworthy and extensible way for a user to
> delegate real tasks to AI while retaining control over what the AI is
> allowed to do.**
