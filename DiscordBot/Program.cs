﻿using Discord;
using Discord.Audio;
using Discord.Commands;
using Discord.WebSocket;
using DiscordBot.Services;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.ServiceProcess;
using log4net;
using DiscordBot.YoutubeDownlaoder;

[assembly: log4net.Config.XmlConfigurator(Watch = true)]
namespace DiscordBot
{

    public class Program
    {
        private static readonly ILog _log = LogManager.GetLogger(typeof(Program));
        private CommandService commands;
        private DiscordSocketClient client;
        private EventService eventService;
        private IServiceProvider services;

        static void Main(string[] args)
        {
            if (Environment.UserInteractive)
            {
                var x = new Program();
                x.MainAsync().GetAwaiter().GetResult();
            }
            else
            {
                using(var service = new ServiceBase())
                {
                    var x = new Program();
                    x.MainAsync().GetAwaiter().GetResult();

                    ServiceBase.Run(service);
                }
            }
        }
    

        public async Task MainAsync()
        {
            client = new DiscordSocketClient();

            client.Log += Log;

            client.Ready += () =>
            {
                _log.Info("Bot connected");

                return Task.CompletedTask;
            };

            

            commands = new CommandService();

            string token = "MzQ0ODgyNjI3NzU0NDU5MTM3.XUSD9w.RAyxs-e6V-onkVHolyJLqV5PxwI"; // Remember to keep this private!

            IYoutubeDownloaderClient youtubeDownloaderClient = new YoutubeExplodeClient();
            AudioService audioService = new AudioService(client, youtubeDownloaderClient);
            eventService = new EventService(client);

            services = new ServiceCollection()
                .AddSingleton(commands)
                .AddSingleton(audioService)
                .AddSingleton(eventService)
                .BuildServiceProvider();

            // Hook the MessageReceived Event into our Command Handler
            client.MessageReceived += HandleCommand;

            // Discover all of the commands in this assembly and load them.
            await commands.AddModulesAsync(Assembly.GetEntryAssembly(), services);

            await client.LoginAsync(TokenType.Bot, token);
            await client.StartAsync();

            // Block this task until the program is closed.
            await Task.Delay(-1);
        }

        private Task Log(LogMessage msg)
        {
            _log.Info(msg.ToString());
            return Task.CompletedTask;
        }

        private async Task HandleCommand(SocketMessage messageParam)
        {
            // Don't process the command if it was a System Message
            var message = messageParam as SocketUserMessage;
            if (message == null) return;
            // Create a number to track where the prefix ends and the command begins
            int argPos = 0;
            // Determine if the message is a command, based on if it starts with '!' or a mention prefix
            if (!(message.HasCharPrefix('!', ref argPos) || message.HasMentionPrefix(client.CurrentUser, ref argPos))) return;
            // Create a Command Context
            var context = new CommandContext(client, message);
            // Execute the command. (result does not indicate a return value, 
            // rather an object stating if the command executed successfully)
            var result = await commands.ExecuteAsync(context, argPos, services);
            if (!result.IsSuccess)
                await context.Channel.SendMessageAsync(result.ErrorReason);
        }

       
    }
}
