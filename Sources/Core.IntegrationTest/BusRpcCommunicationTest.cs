﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using MessageBus.Core;
using MessageBus.Core.API;
using NUnit.Framework;

namespace Core.IntegrationTest
{

    [TestFixture]
    public class BusRpcCommunicationTest
    {
        [Test]
        public void Bus_MakeRpcCall()
        {
            using (IBus bus = new RabbitMQBus(c => c.SetReceiveSelfPublish()))
            {
                using (ISubscriber subscriber = bus.CreateSubscriber())
                {
                    subscriber.Subscribe((RequestMessage m) => new ResponseMessage
                    {
                        Code = m.Data.Length
                    });

                    subscriber.Open();

                    using (IRpcPublisher rpcPublisher = bus.CreateRpcPublisher())
                    {
                        ResponseMessage response;
                        
                        bool res = rpcPublisher.Send(new RequestMessage
                        {
                            Data = "Hello, world!"
                        }, TimeSpan.FromSeconds(10), out response);

                        res.Should().BeTrue();

                        response.ShouldBeEquivalentTo(new ResponseMessage
                        {
                            Code = 13
                        });
                    }
                }
            }
        }
        
        [Test]
        public void Bus_MultiClientRpcCall()
        {
            using (IBus serviceBus = new RabbitMQBus(), client1Bus = new RabbitMQBus(), client2Bus = new RabbitMQBus())
            {
                using (ISubscriber subscriber = serviceBus.CreateSubscriber())
                {
                    subscriber.Subscribe((RequestMessage m) => new ResponseMessage
                    {
                        Code = int.Parse(m.Data)
                    });

                    subscriber.Open();

                    const int calls = 20;

                    using (IRpcPublisher rpcPublisher1 = client1Bus.CreateRpcPublisher(), rpcPublisher2 = client2Bus.CreateRpcPublisher())
                    {
                        List<ResponseMessage> c1Responses = new List<ResponseMessage>(), c2Responses = new List<ResponseMessage>();

                        Task t1 = Task.Factory.StartNew(() =>
                        {
                            for (int i = 0; i < calls; i++)
                            {
                                ResponseMessage response;

                                bool res = rpcPublisher1.Send(new RequestMessage
                                {
                                    Data = "1"
                                }, TimeSpan.FromSeconds(10), out response);

                                if (res)
                                {
                                    c1Responses.Add(response);
                                }
                            }
                        });
                        
                        Task t2 = Task.Factory.StartNew(() =>
                        {
                            for (int i = 0; i < calls; i++)
                            {
                                ResponseMessage response;

                                bool res = rpcPublisher2.Send(new RequestMessage
                                {
                                    Data = "2"
                                }, TimeSpan.FromSeconds(10), out response);

                                if (res)
                                {
                                    c2Responses.Add(response);
                                }
                            }
                        });

                        Task.WaitAll(t1, t2);

                        c1Responses.Should().HaveCount(calls);
                        c2Responses.Should().HaveCount(calls);

                        c1Responses.All(message => message.Code == 1).Should().BeTrue();
                        c2Responses.All(message => message.Code == 2).Should().BeTrue();
                    }
                }
            }
        }
    }

    public class RequestMessage
    {
        public string Data { get; set; }
    }

    public class ResponseMessage
    {
        public int Code { get; set; }
    }
}