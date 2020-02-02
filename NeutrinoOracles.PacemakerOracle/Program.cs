using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using NeutrinoOracles.Common.Converters;
using NeutrinoOracles.Common.Helpers;
using NeutrinoOracles.Common.Models;
using NeutrinoOracles.PacemakerOracle.Enums;
using NeutrinoOracles.PacemakerOracle.Models;
using NeutrinoOracles.PacemakerOracle.Services;
using Newtonsoft.Json.Linq;
using WavesCS;

namespace NeutrinoOracles.PacemakerOracle
{
    class Program
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        private static async Task Main(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
#if DEBUG
                .AddJsonFile("appsettings.development.json", optional: true, reloadOnChange: true)
#elif RELEASE
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
#endif
                .AddCommandLine(args)
                .Build();
            
            var settings = configuration.Get<Settings>();
            var seed = args[0];
            
            var wavesHelper = new WavesHelper(settings.NodeUrl, settings.ChainId);
            var account = PrivateKeyAccount.CreateFromSeed(seed, settings.ChainId);
            var node = new Node(settings.NodeUrl, settings.ChainId);
            
            var commands = new List<Command>()
            {
                Command.Withdraw,
                Command.RebalanceLeasing
            };
            
            var pacemakerService = new PacemakerService(wavesHelper, node, account, settings.NeutrinoSettings, settings.Leasing);
            
            while (true)
            {
                foreach (var command in commands)
                {
                    Logger.Info($"Run command:{command}");
                    var sw = new Stopwatch();
                    sw.Start();
                    
                    try
                    {
                        await RunCommand(pacemakerService, command);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex);
                    }
                    
                    sw.Stop();
                    Logger.Info($"Commands completed in {sw.ElapsedMilliseconds/1000} seconds");
                }   
                
                Logger.Info($"Wait {TimeSpan.FromSeconds(settings.TimeoutSec)} seconds");
                await Task.Delay(TimeSpan.FromSeconds(settings.TimeoutSec));
            }
        }

        private static async Task RunCommand(PacemakerService pacemakerService, Command command)
        {
            await pacemakerService.InitOrUpdate();
            
            switch (command)
            {
                case Command.Withdraw:
                    await pacemakerService.WithdrawAllUser();
                    break;
                case Command.RebalanceLeasing:
                    await pacemakerService.RebalanceLeasing();
                    break;
                case Command.TransferToAuction:
                    await pacemakerService.TransferToAuction();
                    break;
                case Command.ExecuteOrderLiquidation:
                    await pacemakerService.ExecuteOrderLiquidation();
                    break;
                case Command.ExecuteOrderAuction:
                    await pacemakerService.ExecuteOrderAuction();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(command), command, null);
            }
        }
    }
}