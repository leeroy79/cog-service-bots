﻿using System;
using System.Configuration;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using FaceRecogniseBot.Config;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Bot.Connector;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace FaceRecogniseBot.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DirectLineTokenController : ControllerBase
    {
        private string _directLineSecret;

        public DirectLineTokenController(IOptions<BotAuthConfig> options)
        {
            _directLineSecret = options.Value.DirectLineSecret;
        }

        [HttpPost]
        public async Task<ChatConfig> Post()
        {
            HttpClient client = new HttpClient();

            HttpRequestMessage request = new HttpRequestMessage(
                HttpMethod.Post,
                $" https://directline.botframework.com/v3/directline/tokens/generate");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _directLineSecret);
            var userId = $"dl_{Guid.NewGuid()}";

            request.Content = new StringContent(
                JsonConvert.SerializeObject(
                    new { User = new { Id = userId } }),
                    Encoding.UTF8,
                    "application/json");

            var response = await client.SendAsync(request);
            string token = String.Empty;

            if (response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                token = JsonConvert.DeserializeObject<DirectLineToken>(body).token;
            }

            var config = new ChatConfig()
            {
                Token = token,
                UserId = userId
            };

            return config;
        }
    }



    public class ChatConfig
    {
        public string Token { get; set; }
        public string UserId { get; set; }
    }

    public class DirectLineToken
    {
        public string conversationId { get; set; }
        public string token { get; set; }
        public int expires_in { get; set; }
    }


}