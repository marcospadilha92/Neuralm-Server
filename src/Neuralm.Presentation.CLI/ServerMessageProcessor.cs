﻿using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading.Tasks;
using Neuralm.Application.Interfaces;
using Neuralm.Application.Messages;
using Neuralm.Application.Messages.Requests;
using Neuralm.Infrastructure.Interfaces;
using Neuralm.Mapping;
using Neuralm.Utilities.Observer;

namespace Neuralm.Presentation.CLI
{
    internal class ServerMessageProcessor : IMessageProcessor
    {
        private readonly IUserService _userService;
        private readonly ITrainingRoomService _trainingRoomService;
        private readonly MessageToServiceMapper _messageToServiceMapper;
        private readonly ConcurrentDictionary<Type, ObserverCollection> _observers;

        public ServerMessageProcessor(
            IUserService userService,
            ITrainingRoomService trainingRoomService,
            MessageToServiceMapper messageToServiceMapper)
        {
            _userService = userService;
            _trainingRoomService = trainingRoomService;
            _messageToServiceMapper = messageToServiceMapper;
            _observers = new ConcurrentDictionary<Type, ObserverCollection>();
        }

        public IDisposable Subscribe(Type type, IObserver observer)
        {
            if (_observers.ContainsKey(type))
                _observers[type].Add(observer);
            else
                _observers.TryAdd(type, new ObserverCollection(observer));

            return new ObserverUnsubscriber(_observers[type], observer);
        }

        public async Task<IResponse> ProcessRequest(Type type, IRequest request, INetworkConnector networkConnector)
        {
            object response;
            Console.WriteLine($"ProcessRequest: {request}");
            if (_messageToServiceMapper.MessageToServiceMap.TryGetValue(type, out (object service, MethodInfo methodInfo) a))
            {
                dynamic task = a.methodInfo.Invoke(a.service, new object[] { request });
                response = await task;
            }
            else
                throw new ArgumentOutOfRangeException(nameof(request), $"Unknown Request message of type: {type.Name}");

            Console.WriteLine($"ProcessRequest-Response: {response}");
            // ReSharper disable once PossibleInvalidCastException
            return (IResponse)response;
        }
        public Task ProcessCommand(Type type, ICommand command, INetworkConnector networkConnector)
        {
            return Task.Run(() =>
            {
                Console.WriteLine($"ProcessCommand: {command}");
                switch (command)
                {
                    default:
                        throw new ArgumentOutOfRangeException(nameof(command), $"Unknown Command message of type: {type.Name}");
                }
            });
        }
        public Task ProcessResponse(Type type, IResponse response, INetworkConnector networkConnector)
        {
            return Task.Run(() =>
            {
                Console.WriteLine($"ProcessResponse: {response}");
                switch (response)
                {
                    default:
                        throw new ArgumentOutOfRangeException(nameof(response), $"Unknown Response message of type: {type.Name}");
                }
            });
        }
        public Task ProcessEvent(Type type, IEvent @event, INetworkConnector baseNetworkConnector)
        {
            return Task.Run(() =>
            {
                Console.WriteLine($"ProcessEvent: {@event}");
                switch (@event)
                {
                    default:
                        throw new ArgumentOutOfRangeException(nameof(@event), $"Unknown Event message of type: {type.Name}");
                }
            });
        }
    }
}
