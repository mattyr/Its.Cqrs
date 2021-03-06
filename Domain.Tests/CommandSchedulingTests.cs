// Copyright (c) Microsoft. All rights reserved. 
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Reactive.Disposables;
using FluentAssertions;
using Microsoft.Its.Domain.Testing;
using Microsoft.Its.Recipes;
using NUnit.Framework;
using Sample.Domain;
using Sample.Domain.Ordering;
using Sample.Domain.Ordering.Commands;

namespace Microsoft.Its.Domain.Tests
{
    [TestFixture]
    public class CommandSchedulingTests
    {
        private CompositeDisposable disposables;
        private Scenario scenario;

        [SetUp]
        public void SetUp()
        {
            // disable authorization
            Command<Order>.AuthorizeDefault = (o, c) => true;
            Command<CustomerAccount>.AuthorizeDefault = (o, c) => true;

            disposables = new CompositeDisposable { VirtualClock.Start() };

            var customerId = Any.Guid();
            scenario = new ScenarioBuilder(c => c.UseInMemoryEventStore()
                                                 .UseInMemoryCommandScheduling())
                .AddEvents(new EventSequence(customerId)
                {
                    new CustomerAccount.UserNameAcquired
                    {
                        UserName = Any.Email()
                    }
                }.ToArray())
                .Prepare();

            disposables.Add(scenario);
        }

        [TearDown]
        public void TearDown()
        {
            disposables.Dispose();
        }

        [Test]
        public void When_a_commands_is_scheduled_for_later_execution_then_a_CommandScheduled_event_is_added()
        {
            var order = CreateOrder();

            order.Apply(new ShipOn(shipDate: Clock.Now().AddMonths(1).Date));

            var lastEvent = order.PendingEvents.Last();
            lastEvent.Should().BeOfType<CommandScheduled<Order>>();
            lastEvent.As<CommandScheduled<Order>>().Command.Should().BeOfType<Ship>();
        }

        [Test]
        public void Scheduled_commands_are_reserialized_and_invoked_by_a_command_scheduler()
        {
            // arrange
            var bus = new FakeEventBus();
            var shipmentId = Any.AlphanumericString(10);
            var repository = new InMemoryEventSourcedRepository<Order>(bus: bus);

            var scheduler = new InMemoryCommandScheduler<Order>(repository);
            bus.Subscribe(scheduler);
            var order = CreateOrder();

            order.Apply(new ShipOn(shipDate: Clock.Now().AddMonths(1).Date)
            {
                ShipmentId = shipmentId
            });
            repository.Save(order);

            // act
            VirtualClock.Current.AdvanceBy(TimeSpan.FromDays(32));

            //assert 
            order = repository.GetLatest(order.Id);
            var lastEvent = order.Events().Last();
            lastEvent.Should().BeOfType<Order.Shipped>();
            order.ShipmentId.Should().Be(shipmentId, "Properties should be transferred correctly from the serialized command");
        }

        [Test]
        public void InMemoryCommandScheduler_executes_scheduled_commands_immediately_if_no_due_time_is_specified()
        {
            // arrange
            var bus = new InProcessEventBus();
            var repository = new InMemoryEventSourcedRepository<Order>(bus: bus);
            var scheduler = new InMemoryCommandScheduler<Order>(repository);
            bus.Subscribe(scheduler);
            var order = CreateOrder();

            // act
            order.Apply(new ShipOn(Clock.Now().Subtract(TimeSpan.FromDays(2))));
            repository.Save(order);

            //assert 
            order = repository.GetLatest(order.Id);
            var lastEvent = order.Events().Last();
            lastEvent.Should().BeOfType<Order.Shipped>();
        }

        [Test]
        public void When_a_scheduled_command_fails_validation_then_a_failure_event_can_be_recorded_in_HandleScheduledCommandException_method()
        {
            // arrange
            var order = CreateOrder(customerAccountId: scenario.GetLatest<CustomerAccount>().Id);
            // by the time Ship is applied, it will fail because of the cancellation
            order.Apply(new ShipOn(shipDate: Clock.Now().AddMonths(1).Date));
            order.Apply(new Cancel());
            scenario.Save(order);

            // act
            VirtualClock.Current.AdvanceBy(TimeSpan.FromDays(32));

            //assert 
            order = scenario.GetLatest<Order>();
            var lastEvent = order.Events().Last();
            lastEvent.Should().BeOfType<Order.ShipmentCancelled>();
        }

        [Test]
        public void When_applying_a_scheduled_command_throws_unexpectedly_then_further_command_scheduling_is_not_interrupted()
        {
            // arrange
            var customerAccountId = scenario.GetLatest<CustomerAccount>().Id;
            var order1 = CreateOrder(customerAccountId: customerAccountId)
                .Apply(new ShipOn(shipDate: Clock.Now().AddMonths(1).Date))
                .Apply(new Cancel());
            scenario.Save(order1);
            var order2 = CreateOrder(customerAccountId: customerAccountId)
                .Apply(new ShipOn(shipDate: Clock.Now().AddMonths(1).Date));
            scenario.Save(order2);

            // act
            VirtualClock.Current.AdvanceBy(TimeSpan.FromDays(32));

            // assert 
            order1 = scenario.GetLatest<Order>(order1.Id);
            var lastEvent = order1.Events().Last();
            lastEvent.Should().BeOfType<Order.ShipmentCancelled>();

            order2 = scenario.GetLatest<Order>(order2.Id);
            lastEvent = order2.Events().Last();
            lastEvent.Should().BeOfType<Order.Shipped>();
        }

        [Test]
        public void A_command_can_be_scheduled_against_another_aggregate()
        {
            var order = new Order(
                new CreateOrder(Any.FullName())
                {
                    CustomerId = scenario.GetLatest<CustomerAccount>().Id
                })
                .Apply(new AddItem
                {
                    ProductName = Any.Word(),
                    Price = 12.99m
                })
                .Apply(new Cancel());
            scenario.Save(order);

            var customerAccount = scenario.GetLatest<CustomerAccount>();

            customerAccount.Events()
                           .Last()
                           .Should()
                           .BeOfType<CustomerAccount.OrderCancelationConfirmationEmailSent>();
        }

        [Test]
        public void If_Schedule_is_dependent_on_an_event_with_no_aggregate_id_then_it_throws()
        {
            var scheduler = new ImmediateCommandScheduler<CustomerAccount>(new InMemoryEventSourcedRepository<CustomerAccount>());

            Action schedule = () => scheduler.Schedule(
                Any.Guid(),
                new SendOrderConfirmationEmail(Any.Word()),
                deliveryDependsOn: new Order.Created
                {
                    AggregateId = Guid.Empty,
                    ETag = Any.Word()
                });

            schedule.ShouldThrow<ArgumentException>()
                .And
                .Message
                .Should().Contain("An AggregateId must be set on the event on which the scheduled command depends.");
        }

        [Test]
        public void If_Schedule_is_dependent_on_an_event_with_no_ETag_then_it_sets_one()
        {
            var scheduler = new ImmediateCommandScheduler<CustomerAccount>(new InMemoryEventSourcedRepository<CustomerAccount>());

            var created = new Order.Created
            {
                AggregateId = Any.Guid(), 
                ETag = null
            };

            scheduler.Schedule(
                Any.Guid(),
                new SendOrderConfirmationEmail(Any.Word()),
                deliveryDependsOn: created);

            created.ETag.Should().NotBeNullOrEmpty();
        }

        public static Order CreateOrder(
            DateTimeOffset? deliveryBy = null,
            string customerName = null,
            Guid? orderId = null,
            Guid? customerAccountId = null)
        {
            return new Order(
                new CreateOrder(customerName ?? Any.FullName())
                {
                    AggregateId = orderId ?? Any.Guid(),
                    CustomerId = customerAccountId ?? Any.Guid()
                })
                .Apply(new AddItem
                {
                    Price = 499.99m,
                    ProductName = Any.Words(1, true).Single()
                })
                .Apply(new SpecifyShippingInfo
                {
                    Address = Any.Words(1, true).Single() + " St.",
                    City = "Seattle",
                    StateOrProvince = "WA",
                    Country = "USA",
                    DeliverBy = deliveryBy
                });
        }
    }
}
