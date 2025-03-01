﻿//------------------------------------------------------------------------------
// <copyright file="CmdArgsPackage.cs" company="Company">
//     Copyright (c) Company.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using System.Collections.Generic;
using SmartCmdArgs.Helper;
using SmartCmdArgs.ViewModel;
using SmartCmdArgs.Services;
using SmartCmdArgs.Services.Utils;
using System.Threading;

using Task = System.Threading.Tasks.Task;
using IServiceProvider = System.IServiceProvider;
using ServiceProvider = Microsoft.Extensions.DependencyInjection.ServiceProvider;
using SmartCmdArgs.View;

namespace SmartCmdArgs
{
    /// <summary>
    /// This is the class that implements the package exposed by this assembly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The minimum requirement for a class to be considered a valid package for Visual Studio
    /// is to implement the IVsPackage interface and register itself with the shell.
    /// This package uses the helper classes defined inside the Managed Package Framework (MPF)
    /// to do it: it derives from the Package class that provides the implementation of the
    /// IVsPackage interface and uses the registration attributes defined in the framework to
    /// register itself and its components with the shell. These attributes tell the pkgdef creation
    /// utility what data to put into .pkgdef file.
    /// </para>
    /// <para>
    /// To get loaded into VS, the package must be referred by &lt;Asset Type="Microsoft.VisualStudio.VsPackage" ...&gt; in .vsixmanifest file.
    /// </para>
    /// </remarks>
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration("#110", "#112", Vsix.Version, IconResourceID = 400)] // Info on this package for Help/About
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideToolWindow(typeof(ToolWindow), Window = PackageGuids.guidToolWindowString)]
    [ProvideOptionPage(typeof(CmdArgsOptionPage), "Smart Command Line Arguments", "General", 1000, 1001, false)]
    [ProvideBindingPath]
    [ProvideKeyBindingTable(PackageGuids.guidToolWindowString, 200)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExistsAndFullyLoaded_string, PackageAutoLoadFlags.BackgroundLoad)]
    [Guid(PackageGuids.guidCmdArgsToolWindowPackageString)]
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "pkgdef, VS and vsixmanifest are valid VS terms")]
    public sealed class CmdArgsPackage : AsyncPackage
    {
        /// <summary>
        /// CmdArgsPackage GUID string.
        /// </summary>
        public const string DataObjectCmdJsonFormat = "SmartCommandlineArgs_D11D715E-CBF3-43F2-A1C1-168FD5C48505";
        public const string DataObjectCmdListFormat = "SmartCommandlineArgs_35AD7E71-E0BC-4440-97D9-2E6DA3085BE4";
        public const string SolutionOptionKey = "SmartCommandlineArgsVA"; // Only letters are allowed

        private IVisualStudioHelperService vsHelper;
        private IOptionsSettingsService optionsSettings;
        private ISuoDataService suoDataService;
        private ILifeCycleService lifeCycleService;
        private IVsEventHandlingService vsEventHandling;
        private IFileStorageEventHandlingService fileStorageEventHandling;

        private ToolWindowViewModel toolWindowViewModel;
        private TreeViewModel treeViewModel;

        public static CmdArgsPackage Instance { get; private set; }

        private ServiceProvider serviceProvider;
        public IServiceProvider ServiceProvider => serviceProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="ToolWindow"/> class vsix.
        /// </summary>
        public CmdArgsPackage()
        {
            // Inside this method you can place any initialization code that does not require
            // any Visual Studio service because at this point the package object is created but
            // not sited yet inside Visual Studio environment. The place to do all the other
            // initialization is the Initialize method.

            Debug.Assert(Instance == null, "There can be only be one! (Package)");
            Instance = this;

            // add option keys to store custom data in suo file
            this.AddOptionKey(SolutionOptionKey);

            serviceProvider = ConfigureServices();

            toolWindowViewModel = ServiceProvider.GetRequiredService<ToolWindowViewModel>();
            treeViewModel = ServiceProvider.GetService<TreeViewModel>();

            vsHelper = ServiceProvider.GetRequiredService<IVisualStudioHelperService>();
            optionsSettings = ServiceProvider.GetRequiredService<IOptionsSettingsService>();
            suoDataService = ServiceProvider.GetRequiredService<ISuoDataService>();
            lifeCycleService = ServiceProvider.GetRequiredService<ILifeCycleService>();
            vsEventHandling = ServiceProvider.GetRequiredService<IVsEventHandlingService>();
            fileStorageEventHandling = ServiceProvider.GetRequiredService<IFileStorageEventHandlingService>();
        }

        protected override void Dispose(bool disposing)
        {
            serviceProvider.Dispose();

            base.Dispose(disposing);
        }

        internal Interface GetService<Service, Interface>()
        {
            return (Interface)base.GetService(typeof(Service));
        }

        internal async Task<Interface> GetServiceAsync<Service, Interface>()
        {
            return (Interface)await base.GetServiceAsync(typeof(Service));
        }

        internal Page GetDialogPage<Page>()
            where Page : class
        {
            return GetDialogPage(typeof(Page)) as Page;
        }

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await InitializeServicesAsync();

            // we want to know about changes to the solution state even if the extension is disabled
            // so we can update our interface
            vsEventHandling.AttachToSolutionEvents();

            // has to be registered here to listen to settings changes even if the extension is disabled
            // so we can reload them if neccessary to give the user the correct values if he wants to enable the extension
            fileStorageEventHandling.AttachToEvents();

            // Switch to main thread
            await JoinableTaskFactory.SwitchToMainThreadAsync();

            // Extension window was opend while a solution is already open
            if (vsHelper.IsSolutionOpen)
            {
                Logger.Info("Package.Initialize called while solution was already open.");

                lifeCycleService.InitializeConfigForSolution();
            }

            lifeCycleService.UpdateDisabledScreen();

            toolWindowViewModel.UseMonospaceFont = optionsSettings.UseMonospaceFont;
            toolWindowViewModel.DisplayTagForCla = optionsSettings.DisplayTagForCla;

            await base.InitializeAsync(cancellationToken, progress);
        }

        private ServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            services.AddSingleton<Commands>();
            services.AddLazySingleton(x => GetDialogPage<CmdArgsOptionPage>());
            services.AddLazySingleton<SettingsViewModel>();
            services.AddLazySingleton<ToolWindowViewModel>();
            services.AddLazySingleton<TreeViewModel>();
            services.AddLazySingleton<IProjectConfigService, ProjectConfigService>();
            services.AddSingleton<IVisualStudioHelperService, VisualStudioHelperService>();
            services.AddSingleton<IFileStorageService, FileStorageService>();
            services.AddSingleton<IOptionsSettingsService, OptionsSettingsService>();
            services.AddSingleton<IViewModelUpdateService, ViewModelUpdateService>();
            services.AddSingleton<ISuoDataService, SuoDataService>();
            services.AddSingleton<IItemPathService, ItemPathService>();
            services.AddSingleton<IItemEvaluationService, ItemEvaluationService>();
            services.AddSingleton<IItemAggregationService, ItemAggregationService>();
            services.AddSingleton<ISettingsService, SettingsService>();
            services.AddLazySingleton<ILifeCycleService, LifeCycleService>();
            services.AddSingleton<IVsEventHandlingService, VsEventHandlingService>();
            services.AddSingleton<IFileStorageEventHandlingService, FileStorageEventHandlingService>();
            services.AddSingleton<IOptionsSettingsEventHandlingService, OptionsSettingsEventHandlingService>();
            services.AddSingleton<ITreeViewEventHandlingService, TreeViewEventHandlingService>();
            services.AddLazySingleton<IToolWindowHistory, ToolWindowHistory>();

            var asyncInitializableServices = services
                .Where(x => x.Lifetime == ServiceLifetime.Singleton)
                .Where(x => typeof(IAsyncInitializable).IsAssignableFrom(x.ImplementationType))
                .ToList();

            foreach (var service in asyncInitializableServices)
            {
                services.AddSingleton(x => x.GetRequiredService(service.ServiceType) as IAsyncInitializable);
            }

            return services.BuildServiceProvider();
        }

        private async Task InitializeServicesAsync()
        {
            var initializableServices = ServiceProvider.GetServices<IAsyncInitializable>();
            foreach (var service in initializableServices)
            {
                await service.InitializeAsync();
            }
        }

        protected override WindowPane InstantiateToolWindow(Type toolWindowType)
        {
            if (toolWindowType == typeof(ToolWindow))
                return new ToolWindow(toolWindowViewModel, treeViewModel) { Package = this };
            else
                return base.InstantiateToolWindow(toolWindowType);
        }

        protected override void OnLoadOptions(string key, Stream stream)
        {
            base.OnLoadOptions(key, stream);

            if (key == SolutionOptionKey)
            {
                suoDataService.LoadFromStream(stream);
            }
        }

        protected override void OnSaveOptions(string key, Stream stream)
        {
            base.OnSaveOptions(key, stream);
            if (key == SolutionOptionKey)
            {
                if (lifeCycleService.IsEnabled)
                    suoDataService.Update();

                suoDataService.SaveToStream(stream);
            }
        }

        public List<string> GetProjectConfigurations(Guid projGuid)
        {
            var project = vsHelper.HierarchyForProjectGuid(projGuid);

            var configs = (project.GetProject()?.ConfigurationManager?.ConfigurationRowNames as Array)?.Cast<string>().ToList();
            return configs ?? new List<string>();
        }

        public List<string> GetProjectPlatforms(Guid projGuid)
        {
            var project = vsHelper.HierarchyForProjectGuid(projGuid);

            var platforms = (project.GetProject()?.ConfigurationManager?.PlatformNames as Array)?.Cast<string>().ToList();
            return platforms ?? new List<string>();
        }

        public List<string> GetLaunchProfiles(Guid projGuid)
        {
            var project = vsHelper.HierarchyForProjectGuid(projGuid);

            List<string> launchProfiles = null;
            if (project?.IsCpsProject() == true)
            {
                launchProfiles = CpsProjectSupport.GetLaunchProfileNames(project.GetProject())?.ToList();
            }

            return launchProfiles ?? new List<string>();
        }
    }
}
