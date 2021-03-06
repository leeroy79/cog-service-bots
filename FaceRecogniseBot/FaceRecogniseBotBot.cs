﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;

namespace FaceRecogniseBot
{
    /// <summary>
    /// Represents a bot that processes incoming activities.
    /// For each user interaction, an instance of this class is created and the OnTurnAsync method is called.
    /// This is a Transient lifetime service.  Transient lifetime services are created
    /// each time they're requested. For each Activity received, a new instance of this
    /// class is created. Objects that are expensive to construct, or have a lifetime
    /// beyond the single turn, should be carefully managed.
    /// For example, the <see cref="MemoryStorage"/> object and associated
    /// <see cref="IStatePropertyAccessor{T}"/> object are created with a singleton lifetime.
    /// </summary>
    /// <seealso cref="https://docs.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-2.1"/>
    public class FaceRecogniseBotBot : IBot
    {
        private readonly FaceRecogniseBotAccessors _accessors;
        private readonly ILogger _logger;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="accessors">A class containing <see cref="IStatePropertyAccessor{T}"/> used to manage state.</param>
        /// <param name="loggerFactory">A <see cref="ILoggerFactory"/> that is hooked to the Azure App Service provider.</param>
        /// <seealso cref="https://docs.microsoft.com/en-us/aspnet/core/fundamentals/logging/?view=aspnetcore-2.1#windows-eventlog-provider"/>
        public FaceRecogniseBotBot(FaceRecogniseBotAccessors accessors, ILoggerFactory loggerFactory)
        {
            if (loggerFactory == null)
            {
                throw new System.ArgumentNullException(nameof(loggerFactory));
            }

            _logger = loggerFactory.CreateLogger<FaceRecogniseBotBot>();
            _logger.LogTrace("Turn start.");
            _accessors = accessors ?? throw new System.ArgumentNullException(nameof(accessors));
        }

        /// <summary>
        /// Every conversation turn for our Echo Bot will call this method.
        /// There are no dialogs used, since it's "single turn" processing, meaning a single
        /// request and response.
        /// </summary>
        /// <param name="turnContext">A <see cref="ITurnContext"/> containing all the data needed
        /// for processing this conversation turn. </param>
        /// <param name="cancellationToken">(Optional) A <see cref="CancellationToken"/> that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A <see cref="Task"/> that represents the work queued to execute.</returns>
        /// <seealso cref="BotStateSet"/>
        /// <seealso cref="ConversationState"/>
        /// <seealso cref="IMiddleware"/>
        public async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default(CancellationToken))
        {
            var botState = await _accessors.FaceRecogniseBotState.GetAsync(turnContext, () => new FaceRecogniseBotState());

            // Echo back to the user whatever they typed.
            if (turnContext.Activity.Type == ActivityTypes.Message)
            {
                var responseMessage = $"{botState.CurrentUserName} said '{turnContext.Activity.Text}'\n";
                await turnContext.SendActivityAsync(responseMessage);
            }
            // Received event to start conversation.
            else if (turnContext.Activity.Type == ActivityTypes.Event)
            {
                if (turnContext.Activity.Name == Constants.BotEvents.FacesAnalysed)
                {
                    // Persist user ID and name.
                    var split = turnContext.Activity.Value.ToString().Split(";");

                    if (split.Length == 2)
                    {
                        botState.CurrentUserId = split[0];
                        botState.CurrentUserName = split[1];
                        await _accessors.FaceRecogniseBotState.SetAsync(turnContext, botState);
                        await _accessors.ConversationState.SaveChangesAsync(turnContext);

                        await turnContext.SendActivityAsync($"Hi **{botState.CurrentUserName}**!");
                    }
                }

                // Emotion detected. If it has changed, record it.
                if (turnContext.Activity.Name == Constants.BotEvents.NewEmotion)
                {
                    var emotion = turnContext.Activity.Value.ToString();

                    if(!string.IsNullOrEmpty(emotion))
                    {
                        if(emotion != botState.Emotion)
                        {
                            botState.Emotion = emotion;
                            await _accessors.FaceRecogniseBotState.SetAsync(turnContext, botState);
                            await _accessors.ConversationState.SaveChangesAsync(turnContext);

                            _logger.LogInformation($"Emotion detected - **{emotion}**");

                            var emotionMessage = GetEmotionMessage(emotion);

                            if (!string.IsNullOrWhiteSpace(emotionMessage))
                            {
                                await turnContext.SendActivityAsync(emotionMessage);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Gets a message suitable for the detected emtion.
        /// </summary>
        /// <param name="emotion">The emotion.</param>
        /// <returns></returns>
        private string GetEmotionMessage(string emotion)
        {
            var message = string.Empty;

            if (!string.IsNullOrWhiteSpace(emotion))
            {
                switch(emotion.Trim().ToLower())
                {
                    case "anger":
                    case "disgust":
                    case "contempt":
                    case "sadness":
                        message = "You don't seem entirely pleased. Would you like to provide some feedback?";
                        break;
                    case "surprise":
                    case "fear":
                        message = "You seem like you need help. Would you like for me to explain what I can help you with?";
                        break;
                    case "happiness":
                        message = "You seem happy! Would you like to leave a 5 star review?";
                        break;
                }
            }

            return message;
        }
    }
}
