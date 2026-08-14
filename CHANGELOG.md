# Release History

This file contains the consolidated release history for the `rgf-client` solution and its published packages.

## [10.3.0] - 2026-08-14

### Features Added

- Extended chart and aggregation capabilities.
  - Added a Card chart type for compact single-value aggregate visualization.
  - Added aggregate-based sorting with configurable priority and direction.
  - Added result limiting for aggregate queries.
  - Added Date and DateTime support for `Min` and `Max` aggregations.
  - Added `RGO_AutoExternal` fields to chart grouping and aggregation selectors.
- Added configurable loading indicators for entity and dashboard loading states, including custom component support.
- Added portable dashboard exports that bundle referenced Grid and Chart settings and use stable saved-view references across environments.

### Other Changes

- Unified culture-aware date and numeric formatting across Grid, Chart and Card views, including chart labels, axes, tooltips and legends.
- Reduced toast noise during chart data loading, rendering, redraw and settings application in embedded and dashboard contexts.
- Improved dashboard model normalization, validation and serialization for portable definitions.
- Updated Blazor-ApexCharts from 6.1.0 to 7.0.0.

### Bugs Fixed

- Fixed Grid and Chart data handling.
  - Fixed Date and DateTime chart grouping, subgroup sorting and labeling.
  - Fixed chart settings validation after entity metadata changes.
- Fixed authentication and proxy handling.
  - Fixed SessionAuth protected-route reauthentication and redirect handling after session revalidation.
  - Fixed OpenID Connect host proxy forwarding of RGF client version and session headers.

### Breaking Changes

- Updated aggregation contracts.
  - Replaced `RgfAggregationSettings.SubGroup` with `SubGroups`. `SubGroup` is now obsolete and produces a compile-time error.
  - `RgfAggregationSettings.Groups` and `SubGroups` are now nullable. Use `GroupsOrEmpty` and `SubGroupsOrEmpty` when modifying these collections.
- Updated filter contracts.
  - Replaced `RgfFilter.Condition.PropertyId` with the inherited `Id` and `Alias` identifiers. `PropertyId` is now obsolete and produces a compile-time error.
- RGF Client 10.3 requires RGF.Core 10.3 or later


## [10.2.0] - 2026-07-16

### Features Added

- Added comprehensive dashboard support.
  - Added nested row/column layouts, resizable panes, role-based visibility, runtime rendering and the `/rgf/dashboard` page.
  - Added a visual dashboard designer for creating, cloning, editing and deleting dashboards, including pane splitting, resizing and saved-view assignment.
  - Added support for displaying saved Grid, Tree, Chart and Chart Data views in dashboard panels.
  - Added dashboard catalog, entity/view discovery, loading, saving and deleting support.
  - Added permission control for managing public dashboards.
- Extended saved-view support.
  - Grids and charts can now start from predefined saved settings.
  - Entities can initialize without performing the initial list data request, enabling more efficient embedded and dashboard scenarios.
- Extended chart support for embedded usage.
  - Added embedded, data-only and hidden-control modes.
  - Added predefined chart settings and layout-aware resizing for dashboard panels.
  - Added chart lifecycle events for custom integrations.
- Added persistent user interface preferences for language, theme and UI size, synchronized with the authenticated user profile.
- Added a Large UI size option alongside the existing Small and Default sizes.
- Added parameterized localization resources with placeholder validation and safer handling of missing, additional or invalid formatting arguments.
- Added a responsive navigation bar with desktop and mobile layouts, configurable branding and breakpoint, custom content, and optional language and theme selectors.

### Other Changes

- Improved embedded chart resizing to avoid unnecessary redraws while dashboard panels are being resized.
- Added reusable dashboard designer, saved-view selection, layout validation and layout manipulation infrastructure for custom dashboard integrations.
- Added public localization formatting helpers for custom localization implementations.

### Bugs Fixed

- Fixed missing user identity information in server-proxy authentication scenarios.
- Fixed parameterized localized messages, including required-field validation and bulk-action messages.
- Fixed chart initialization when no initial chart setting is specified.

### Breaking Changes

- Changed user-state and role contracts.
  - User-state values are now exposed as immutable snapshots.
  - Role collections now use read-only dictionary contracts.
  - `IRecroSecService` gained required user-state notification, access and settings-update members; custom implementations must be updated.
- `IRecroDictService` gained required support for parameterized localized resources; custom implementations must be updated.
- `IRgListHandler` and `IRgFilterHandler` gained a required filter-state application member; custom implementations must be updated.
- Reworked theme and UI size selection.
  - The previous theme selector component was replaced and its theme parameter was renamed.
  - UI size classes now use `rgf-small-ui` / `rgf-large-ui` on the `<html>` element instead of the previous `small` class on `<body>`; custom CSS or JavaScript targeting the previous structure must be updated.
- Navbar rendering moved from the general menu component to the new responsive navbar component; integrations relying on the previous navbar mode must be updated.
- RGF Client 10.2 requires RGF.Core 10.2 or later


## [10.1.0] - 2026-04-24

### Features Added

- Added first-class authentication support for WebAssembly, host-proxy and SSR scenarios.
  - Added the `Recrovit.RecroGridFramework.Client.Blazor.SessionAuth` package with cookie-backed session validation, protected-route handling, principal synchronization and SSR cookie forwarding.
  - Added the `Recrovit.RecroGridFramework.Client.Blazor.Host.OpenIdConnect` package with high-level OpenID Connect host setup, proxy endpoints, middleware integration and Razor Components registration.
  - Added explicit `None`, `WasmBearer`, `ServerProxy` and `ServerProxySsr` authentication modes with separate external and proxy API addresses.
  - Added authentication extensibility contracts for token access, SSR cookie forwarding and unauthorized-response handling.
  - Added explicit handling for `401 Unauthorized` and `403 Forbidden` API responses, with clearer error messages and pluggable reauthentication handling.
  - Added authentication state synchronization across runtime modes, including `RgfUserState.UserName`, proxy identity enrichment and authentication state change notifications.
  - Added host/downstream API invocation contracts and Blazor initialization hooks for runtime-independent integrations.
- Improved Blazor application integration.
  - Added automatic Blazor and UI resource loading through `RgfRootComponent`.
  - Added public resource URL helpers and exposed the minimum supported RGF.Core version for custom Blazor integrations.

### Other Changes

- Theme and UI size selection now initializes from the current page state.
- Added `GetRowsByAbsoluteIndexes` and deprecated the `GetSelectedRowsData` extension method.

### Bugs Fixed

- Fixed authentication initialization races affecting user-language updates and permission loading.
- Fixed Blazor UI update and selection consistency.
  - Fixed entity refresh and rerender operations to run on the Blazor renderer context instead of a background task.
  - Fixed toast refresh, expiry and disposal lifecycle handling.
  - Fixed selected-row highlighting in Grid and Tree views when row or cell CSS classes are present.

### Breaking Changes

- Updated public authentication and API contracts.
  - `IRecroSecService` gained the required `AuthenticationStateChanged` and `UserName` members, and its token, language and permission APIs now use nullable annotations where applicable.
  - `IRgfApiResponse<T>` and `ApiResponse<T>` gained the `ReasonPhrase` member; custom interface implementations must be updated.
  - Direct construction of `ApiService` and `RgfAuthorizationMessageHandler` must be updated because their constructors gained required authentication dependencies; the standard DI registrations provide these automatically.
  - Removed `RgfAuthorizationMessageHandler.LoginPath`; authentication endpoint paths are now resolved by `RgfAuthenticationEndpointResolver` and can be configured through `Authentication:Host:EndpointBasePath`.
- Changed authentication registration and configuration APIs.
  - `RGF.Client` no longer depends directly on WebAssembly authentication; token access is abstracted through `IRgfAccessTokenAccessor`.
  - `AddRgfServices` now supports explicit authentication modes and defaults to `RgfApiAuthMode.None` with a no-op token accessor unless a runtime-specific authentication integration is registered.
  - `AddRgfBlazorServices` is obsolete and now maps to `AddRgfBlazorWasmBearerServices`; applications without authentication should use `AddRgfBlazorWithoutAuthServices`.
- Changed the built-in entity/admin page integration.
  - Replaced the `Entity` and `Legacy` page components with `RgfEntityPage` and `RgfEntityContent`; the `/rgf/entity` and `/rgf/admin` routes remain unchanged.
  - Built-in entity/admin pages now explicitly use Interactive WebAssembly with prerendering disabled.
- RGF Client 10.1 requires RGF.Core 10.1 or later

## [10.0.0] - 2025-12-09

### Features Added

- Updated target framework to .NET 10.
- Added support for .NET 10 compatibility

### Other Changes

- Update BroadcastMessages method with clear option

## [8.20.0] - 2025-06-16

### Features Added

- Enabled selection and filtering of columns from any distant N:1 related entities in the grid without the need for configuration

### Bugs Fixed

- Fixed tooltip and TooltipOptions conflict

### Other Changes

- Improved usage of interface for better abstraction

## [1.19.0] - 2025-06-16
#### Recrovit.RecroGridFramework.Abstraction

### Features Added

- Prepared ColumnSettings for automatic selection of related table columns

## [8.19.4] - 2025-05-11

### Bugs Fixed

#### Recrovit.RecroGridFramework.Client.Blazor.UI
- Ensure RgfHtml element reference is correctly updated during re-render
- Fixed column header tooltips after column reordering in list view
- Fixed grid header rendering on size or sort order change

## [8.19.1] - 2025-05-09

### Other Changes

#### Recrovit.RecroGridFramework.Client.Blazor
- Handled access token failure by redirecting to login with interactive options

## [8.19.0] - 2025-05-02

### Features Added

- Added Footer property and markup generation to RgfToastEventArgs
- Added RecroDictText component for rendering localized text from RecroDict
- Added RecroDict integration to RgfBaseComponent
- Added button to refresh tree view branches
- Added footer support to toast notifications
- Added tooltips to grid column headers

### Bugs Fixed

- Ensured column order was preserved after selecting a view setting
- Fixed toolbar status after failed delete operation
- Fixed handling of progress task toast state
- Cancellation during Manager initialization
- Multiple message dialogs triggered globally
- Unsubscribed old Manager from events before re-creation

### Other Changes

- Improved RecroDict language handling
- Improved display of toast changes

## [1.18.0] - 2025-05-02
#### Recrovit.RecroGridFramework.Abstraction

### Features Added

- Added scopedResourceKey overload to GetItem in RecroDict
- Added Footer to ProgressArgs

## [8.18.0] - 2025-04-02

### Features Added

- Introduced TreeEvent and TreeParameters
- Added RGO_TreeViewExclude option to exclude item from Tree view
- Introduced keysToPrevent parameter to specify keys that block event propagation

### Bugs Fixed

- Fixed null check for hubConnection in RgfProgressService
- Resolve issue with selected items after deletion
- Resolve hierarchical rendering issue on onkeydown event

### Other Changes

- Improved logging
- Enhanced tooltip functionality in ToolbarComponent
- Improved tooltip data assembly
- Changed the access modifier of the `_attributes` field in the RgfBaseComponent

## [1.17.0] - 2025-04-02
#### Recrovit.RecroGridFramework.Abstraction

### Other Changes

- Improved tooltip data assembly
- Enhanced some methods in RgfDynamicDictionary to support an optional ignoreCase parameter for case-insensitive key lookups.

## [8.17.0] - 2025-03-06

### Features Added

- Introduced RgfDialog events and related parameters
- Added RgfLoggerFactory for non-DI components to access loggers
- Added logging support to EventDispatcher
- Added icon handling to MenuComponent
- Added selection capability to tree view
- Added custom menu capability to tree view
- Added new enum VisibilityState and Visibility parameter to RgfBaseComponent
- Implemented splitter supporting both horizontal and vertical orientation
- Introduced SplitterContainer for managing hierarchical splitters
- Implemented ensureVisible function for scrolling to an element and handling visibility focus

### Bugs Fixed

- Fixed header status when the filter was turned off
- Disabled entity selection in non-editable fields
- Improved ComboBox rendering for ReadOnly and Disabled state

### Other Changes

- Improved EventDispatcher to support unsubscribing lambda/anonymous delegates and specific subscribers
- Simplified event raising by introducing RaiseEventAsync method
- Enhanced ObservableProperty implementation for improved functionality
- Enhanced ProgressService and Custom Function to support Background Task API calls
- Removed IsModal parameter and introduced IsInline. The dialog is now either modal or inline.
- Improved tree view selection capability
- Refactored menu creation in RgfToolbarComponent
- Replaced the `ParentManager` property with `ParentEntityParameters`
- Improved event handling to support new EventDispatcher
- Refactored dynamic dialog event handling and parameterization
- Refactored dialog event handling
- Improved Toast component styles
- Expanded RgfTooltipOptions with Delay parameters

## [1.16.0] - 2025-03-06
#### Recrovit.RecroGridFramework.Abstraction

### Features Added

- Added logging support to EventDispatcher
- Added support for unsubscribing all event handlers of a specific subscriber in EventDispatcher

## [8.16.0] - 2025-01-31

### Features Added

- Implemented automatic page refresh upon user creation
- Implemented ProgressService for tracking long-running server processes
- Added automatic and manual progress tracking for server-side CustomFunction calls
- Introduced EventArgs for RgfWrapper
- Added FormItemsFirstRenderCompleted to RgfFormEventKind
- Introduced FormItemsFirstRenderCompleted event
- Implemented "Apply and Next New" functionality for the form view
- Set focus to the first editable field in the form view

### Bugs Fixed

- AppRootPath handling issue
- BaseAddress handling issue
- Improve form positioning

### Other Changes

- Refactored ToastEventArgs creation for ToastEvent removal and resending
- Extended RgfToastEventArgs to support Progress data and allow custom header/body rendering functions
- Improved evaluation of the QuickFilter
- Refactored CallCustomFunctionAsync to use context object
- Renamed DynamicComponentWrapper to RgfComponentWrapper and extended it to support event dispatch
- Refactored ToastComponent to use pre-formatted content
- Update for RGF.Client.Blazor compatibility

## [1.15.0] - 2025-01-31
#### Recrovit.RecroGridFramework.Abstraction

### Features Added

- Added RgfNotMappedAttribute
- Added new enum conversion methods.
- Added IsNewlyCreated property to RgfUserState class
- Added ProgressArgs definition for client-side display of long-running server processes

## [8.15.0] - 2024-12-30

### Features Added

- Introduced DisplayMode with Grid and Tree options
- Prepared the QuickFilter functionality
- Added version compatibility check for RGF Client and RGF Server Core
- Added support for managing Tooltips for properties
- Added tree view functionality
- Integrated version compatibility checks
- Implemented Quick Filter feature

### Bugs Fixed

- Prevent multiple Bootstrap tooltip instances on the same element
- Issue with user-defined PageSize input handling

### Other Changes

- Extracted a base Data component from the RgfGrid component
- Enhanced tooltip functionality with additional parameter options
- Improved efficiency and reliability of basic components
- Improved user experience

## [1.14.0] - 2024-12-30
#### Recrovit.RecroGridFramework.Abstraction

### Features Added

- Added `EnsureContains` method to `StringExtensions` for ensuring values in a string with customizable separator
- Defined version compatibility constants for RGF Client and RGF Server Core
- Added new class RgfPropertyTooltips for managing property tooltips

### Other Changes

- Enhanced EnumExtensions with GetAttributeValue method for retrieving custom attribute values
- Enhanced model classes
- Refactored IRgfApiRequest to delegate version compatibility checks to a separate layer
- Renamed HeaderParam to AdditionalHeaders

## [8.14.0] - 2024-12-13

### Features Added

- Added access to user's public RGF roles in the RecroSec service
- Extended menu parameter with HideOnMouseLeave
- Added tooltip functionality to ui.base components
- Extend RgfComboBox to allow selecting an empty item if not present in the input list
- Implemented HideOnMouseLeave setting to automatically hide the menu

### Other Changes

- Refactored predefined filters to support role-based visibility control
- Updated OnMouseLeave to support boolean return value
- Changed IsPublic to RoleId for visibility control in Chart and Grid
- Changed IsPublic to Role for visibility control in Chart and Grid on the GUI
- Enhanced user experience

## [1.13.0] - 2024-12-13
#### Recrovit.RecroGridFramework.Abstraction

### Features Added

- Added Roles property to RgfUserState and IRecroSecService
- Implemented ICloneable and DeepCopy in various classes

### Other Changes

- Refactored RgfChartSetting and RgfGridSetting classes: removed IsPublic and IsPublicNonNullable, added RoleId for visibility control
- Refactored predefined filters to support role-based visibility control

## [8.13.0] - 2024-12-08

### Features Added

- Implement multi-select support for batch operations
- Added functionality for bulk deletion
- Extended select mode with right-click to open the form directly
- Added methods to remove and manage field error messages in the interface
- Add multi-select functionality to the grid
- Added right-click context menu support to RgfButton
- Enhanced user experience by displaying icons for checkbox types in the grid

### Bugs Fixed

- Fixed issue with refreshing after applying an incorrect filter
- Set the title for the grid dialog used for selections
- Initialization of the responsive Flex view
- Tooltip initialization in the grid

### Other Changes

- Renumbered EventKind enums for consistency
- Renamed the class RgfToastEvent to RgfToastEventArgs
- Renamed class RgfUserMessage to RgfUserMessageEventArgs
- Simplified manager initialization by moving FormOnly and AutoOpenForm to RgfEntityComponent
- Updated column template handling in RgfGridComponent
- Refactored JavaScript calls to eliminate eval usage

## [1.12.0] - 2024-12-08
#### Recrovit.RecroGridFramework.Abstraction

### Features Added

- Added methods to remove and manage field error messages in the interface
- Added GetDateTimeValue method to DictionaryExtensions

### Other Changes

- Modified RgfSelectParam to support multi-row selection instead of single row selection

## [8.12.3] - 2024-11-25

### Bugs Fixed

#### Recrovit.RecroGridFramework.Client.Blazor
- Incorrect PageSize display after loading a setting
- Total Pages recalculation

## [8.12.2] - 2024-11-25

### Bugs Fixed

#### Recrovit.RecroGridFramework.Client
- Grid settings loading
#### Recrovit.RecroGridFramework.Client.Blazor.UI
- Incorrect PageSize display after loading a setting

### Other Changes

#### Recrovit.RecroGridFramework.Client
- Removed redundant ItemsPerPage and PageSize properties from IRgListHandler, as they are accessible via EntityDesc

## [8.12.1] - 2024-11-20

### Bugs Fixed

#### Recrovit.RecroGridFramework.Client.Blazor
- Prevent chart settings modification from overriding predefined settings
- Disable client-side grid aggregation

### Other Changes

#### Recrovit.RecroGridFramework.Client.Blazor.UI
- Improved RgfApexCharts and palette compatibility

## [8.12.0] - 2024-11-19

### Features Added

- Added keyboard navigation to RgfPagerComponent

### Bugs Fixed

- Resolved issues with StaticWebAssetFingerprinting and library bundle.scp.css detection

### Other Changes

- Included filter settings in chart settings save functionality
- Enhanced flexbox layout for better display
- Improved code clarity

## [1.11.0] - 2024-11-18
#### Recrovit.RecroGridFramework.Abstraction

### Features Added

- Added ParentGridSettings property to RgfChartSetting class

### Other Changes

- Changed IsReadonly and PageSize in RgfGridSettings to nullable

## [8.11.0] - 2024-11-13

### Features Added

- Added title property to RgfMenuEventArgs
- Added toast notifications for custom menu selection
- Added shouldLoadBundledStyles configuration parameter for library initialization
- Added Remark property to RgfChartSetting

### Bugs Fixed

- Grid settings reset

### Other Changes

- Replaced ChartOnlyData with AggregationRequired

## [1.10.0] - 2024-11-13
#### Recrovit.RecroGridFramework.Abstraction

### Features Added

- Added Remark property to RgfChartSetting

### Other Changes

- Marked ChartOnlyData as obsolete in PropertyFormType

## [8.10.0] - 2024-11-10

### Features Added

- Added TriggeredAt property to RgfEventArgs
- Defined RgfToastEvent
- Triggered toast events in various locations
- Added `RecroChart` enum value to `RgfToolbarEventKind`
- Added an enumeration `RgfChartEventKind`
- Created `RgfChartEventArgs` class
- Implemented chart settings feature with save and load capabilities
- Introduced GetComponentType method to retrieve the Type of RgfBlazor component
- Introduced parameters for creating a context menu
- Added header menu
- Implemented Aggregates functionality
- Added toast notifications at multiple points
- Enabled access to `RecroChart` from the toolbar
- Added a new boolean property `DeferredInitialization` to the `RgfEntityParameters` class, allowing deferred initialization to be manually triggered later
- Added PromptDeletionConfirmation function
- Implemented ToastComponent
- Added toolbar button for RecroChart
- Created ChartComponent from BaseChartComponent
- Added shouldLoadBundledStyles configuration parameter for library initialization

### Bugs Fixed

- Culture handling for numeric field
- Adjusted dialog negative position issue
- Set button type="button" only when not specified

### Other Changes

- Replaced EventCallback with Action and Func delegates in event notification services and observers
- Replaced `ChartData` with `AggregatedData` across multiple interfaces and classes
- Updated IRgListHandler interface to include Initialized properties
- Refactored IsLoading to ObservableProperty in ListHandler
- Refactored `BroadcastMessages` to be asynchronous
- Updated `InitializeAsync`, `LoadRecroGridAsync`, and `RecreateAsync` methods to return success indicators
- Enhanced grid request management
- Refactored script and stylesheet loading and initialization
- Renamed MenuRenderCallback to OnMenuRender and MenuSelectionCallback to OnMenuItemSelect for consistency
- Updated NotificationManager for compatibility
- Refactored RgfChartComponent and its management for better organization
- Updated jQueryui to 1.14.1
- Disabled GridSetting in ClientMode
- Modified title of SettingsMenu button in toolbar
- Enhanced UI design and user experience

## [1.9.0] - 2024-11-10
#### Recrovit.RecroGridFramework.Abstraction

### Features Added

- Added `TryGetNumericEquality` method for numeric value comparison
- Updated IRgfEventArgs to include a new TriggeredAt property
- Added RgfChartSettings class and RgfChartSeriesType enum for chart configuration
- Added new methods in `EnumExtensions.cs`: `ToDictionary`, `ToNullableDictionary`
- Introduced `RgfIdAliasPair` class with `Id` and `Alias` properties
- Added `ChartOnlyData` to `PropertyFormType` enum in `RgfProperty.cs`
- Added new permission: PublicChartSetting
- Added new enumeration RgfProcessingStatus

### Bugs Fixed

- JSON serialization option in RgfGridSetting

### Other Changes

- Removed receiver parameter from Subscribe method overloads in IRgfNotificationManager
- Replaced ChartData with Aggregation parameter class
- Refactored RgfAggregationSettings and RgfChartSetting classes
- Refactored the `RgfColumnSettings`
- Corrected class names to use plural forms for consistency: `DictionaryExtensions`, `EnumExtensions`, `ICollectionExtensions`
- Enhanced RgfGridRequest and RgfGridResult classes
- Renamed AggregateParam to AggregationSettings in RgfListParam

## [8.9.0] - 2024-10-08

### Features Added

- Refactored and enhanced filter, form, and list handlers for GridSettings save/load functionality
- Added a new event FormEventKind `ParametersSet`
- Added GetRowData method to IRgListHandler
- Enhanced grid settings management
- Added ButtonName property to buttons
- Added event dispatch to ParametersSet in RgfFormComponent
- Added GetColumnData methods to RgfGridComponent
- Added tooltip to grid cell

### Other Changes

- Updated package references
- Improved handling of PredefinedFilter
- Updated jQueryUI to v1.14.0
- Improved custom data handling in ComboBox
- Refactored RgfComboBox: async params, nullable Text
- Implemented IDisposable and enhanced ParametersSet handling in FormComponent

## [1.8.0] - 2024-10-08
#### Recrovit.RecroGridFramework.Abstraction

### Features Added

- Added new permission type: PublicGridSetting
- Added new properties and constructors to RgfGridResult, RgfColumnSettings

### Other Changes

- Improved null checks and exception handling in RgfDynamicDictionary
- Modified constructors in RgfProperty to reduce redundancy
- Refactored RgfGridSettings

## [8.8.0] - 2024-08-16

### Features Added

- Add RgfListBox component

### Bugs Fixed

- DateTime formatting in Grid
- RgfComboBox dispose

### Other Changes

- Improved Filter handler
- Update for RGF.Client compatibility
- Update for RGF.Client.Blazor compatibility
- Refactored RgfComboBox to simplify
- Set Width parameter to fit-content in RgfFilter.LogicalOperator

## [1.7.0] - 2024-08-16
#### Recrovit.RecroGridFramework.Abstraction

### Bugs Fixed

- DateTime conversion

### Other Changes

- Improved RgfDynamicData
- Refactored RgfFilter

## [8.7.2] - 2024-08-05

### Features Added

- Add resizable feature to RgfComboBox component

### Other Changes

- Sort filter properties
- Improved LegacyUI dispose

## [8.7.1] - 2024-07-29

### Bugs Fixed

- DefaultHandlers: event unsubscription
- Event unsubscription

## [8.7.0] - 2024-07-19

### Features Added

- Added ChartData Request/Response
- Added charting capability
- Implemented sorting functionality for grid column settings

### Other Changes

- Improved numerical data formatting
- Improved checkbox styling
- Enhanced draggable modal dialog feature
- Improved FilterUI label and button layout

## [1.6.1] - 2024-07-19
#### Recrovit.RecroGridFramework.Abstraction

### Features Added

- Added Rgf.Chart classes
- Added a new constant `RecroChart` to Menu

### Other Changes

- Enhanced decimal handling in RgfDynamicData
- Enhanced the `GridColumnSettings` class by adding a condition to assign the CSS class

## [8.6.1] - 2024-06-10

### Bugs Fixed

#### Recrovit.RecroGridFramework.Client.Blazor.UI
- Forced loading of CSS isolation

## [8.6.0] - 2024-06-06

### Features Added

- AfterRender Event

### Bugs Fixed

- FormFlexColumn default width
- FormItem class
- Legacy ChkVersion
- Grid: Init.Column.Width

### Other Changes

- Update -> Bootstrap v5.3.3
- Improved Form style

## [8.5.3] - 2024-05-27

### Bugs Fixed

#### Recrovit.RecroGridFramework.Client.Blazor
- Unhandled CustomFunction
#### Recrovit.RecroGridFramework.Client.Blazor.UI
- MenuComponent: sequence

## [8.5.2] - 2024-05-24

### Bugs Fixed

- Form: Cancel
- Form height

### Other Changes

- Updating RGF PackageReferences

## [1.5.2] - 2024-05-24
#### Recrovit.RecroGridFramework.Abstraction

### Other Changes

- CustomFunction:Row-type

## [8.5.1] - 2024-05-18

### Other Changes

#### Recrovit.RecroGridFramework.Client
- Updating RGF PackageReferences

## [1.5.1] - 2024-05-08
#### Recrovit.RecroGridFramework.Abstraction

### Other Changes

- Improving DictionaryExtension

## [8.5.0] - 2024-04-26
### Features Added

- InputText: MaxLength

### Bugs Fixed

- Form group message

### Other Changes

- Rename RgfMessages => RgfCoreMessages
- Improving Form validation

## [1.5.0] - 2024-04-26
#### Recrovit.RecroGridFramework.Abstraction

### Features Added

- FormValidationMessages

### Other Changes

- Improving RgfDynamicDictionary
- Improving DictionaryExtension
- Improving RgfProperty
- Rename RgfMessages => RgfCoreMessages

## [8.4.1] - 2024-04-19

### Bugs Fixed

#### Recrovit.RecroGridFramework.Client.Blazor
- Mixture of Filter and Form EditContext
- The RgfLegacyComponent only works for admins
- The main menu starts with the wrong filter

## [8.4.0] - 2024-03-20

### Features Added

- Handling logical RowIndex
- Record navigation in Form
- Server-side permissions
- Addition of RGProperty styles for Grid display
- Placement options for additional buttons in Dialog
- New button settings in Dialog (IconName, CssClass, Disabled, Title)
- Display of RGF Legacy GUI in Blazor environment (Admin UI)

### Bugs Fixed

- PredefinedFilterAdmin permissions in filter

### Other Changes

- Redesigning event handling (Handled, PreventDefault)
- Redesigning Toolbar and Menu events
- Relocation of event definitions to RGF.Client
- Removal of IRgManager.RecroSec, IRgManager.RecroDict
- Increased performance of embedded grids
- Relocation jQueryUI to RGF.Client.Blazor

## [1.4.0] - 2024-03-20
#### Recrovit.RecroGridFramework.Abstraction

### Features Added

- IRgfEventArgs: Handled, PreventDefault
- IRecroSecService: GetAccessToken
- IRecroSecService: GetEntityPermissions
- Enum Extension: Get EnumMemberAttribute

### Other Changes

- Improving RgfDynamicDictionary
- EventNotificationService: async
- Rename UserRoles => RoleClaim

## [8.3.0] - 2024-02-21

### Features Added

- RGO_FormFlexColumnWidth-{tabIdx}-{groupIdx}
- EntityEditor
- SSR/CSR initialize
- Add FormComponent and GridComponent parameters
- Grid view: CustomMenu
- EntityEvents: Initialized, Destroy
- GridEvents: CreateAttributes, ColumnSettingsChanged
- Observable: GridDataSource
- Save FilterDialofg position

### Bugs Fixed

- FormUpdate(RecroGrid, ImageInDB)
- DialogType
- Fix minor issues

### Other Changes

- Improved RecroDict/UserLanguage
- AppRootUrl => AppRootPath
- IRgListHandler rename: GridData => ListDataSource
- EventDispatcher => async
- ObservableProperty => async
- Rename: GridComponent => BaseGridComponent
- Rename: FormComponent => BaseFormComponent
- Improving RGO_JSRowStyle and RGO_JSColStyle calculations

## [1.3.0] - 2024-02-21
#### Recrovit.RecroGridFramework.Abstraction

### Features Added

- Add RgfProperty Form:Tab,Group,Pos
- RgfDynamicDictionary: CopyTo
- DictionaryExtension: GetIntValue

### Bugs Fixed

- QueryOperator: date

### Other Changes

- EventDispatcher: any TValue
- FlexColumnWidth: no default value

## [8.2.0] - 2024-01-24

### Features Added

- UserLanguage
- MenuTitle
- StylesheetsReferences

### Bugs Fixed

- ApplySelect
- SettingsMenu

### Other Changes

- Improved Logging
- Improved Form:Resizable,SavePosition

## [1.2.0] - 2024-01-24
#### Recrovit.RecroGridFramework.Abstraction

### Features Added

- UserLanguage
- StylesheetsReferences
- MenuTitle

### Bugs Fixed

- Nullable CheckBox
- RgfDynamicDictionary.MemberNames

## [8.1.0] - 2024-01-14

### Features Added

- RGF CustomFunction
- RGF Export

### Bugs Fixed

- Setting default values for fields

### Other Changes

- Improved Spinner

## [1.1.0] - 2024-01-14
#### Recrovit.RecroGridFramework.Abstraction

### Features Added

- RGF CustomFunction
- RGF CultureInfo

### Other Changes

- Improving API abstraction

## [8.0.1] - 2024-01-03

### Bugs Fixed
#### Recrovit.RecroGridFramework.Client.Blazor.UI
- Column resizing

## [8.0.0] - 2024-01-02

- Initial release.

## [1.0.0] - 2023-11-28
#### Recrovit.RecroGridFramework.Abstraction

- Initial release.

