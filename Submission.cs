// CSE445 Assignment 2: Hotel Booking System with Multithreading and Event-Driven Programming

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace ConsoleApp1
{
    // Delegate declaration for creating events
    public delegate void PriceCutEvent(double roomPrice, Thread agentThread);
    public delegate void OrderProcessEvent(Order order, double orderAmount);
    public delegate void OrderCreationEvent();

    public class MainClass
    {
        public static MultiCellBuffer buffer;
        public static Thread[] travelAgentThreads;
        public static bool hotelThreadRunning = true;
        public static void Main(string[] args)
        {
            try
            {
                // Perform necessary preparation
                Console.WriteLine("Inside Main: Preparing the system");
                buffer = new MultiCellBuffer();

                // Instantiate Hotel and TravelAgent objects
                Hotel hotel = new Hotel();
                TravelAgent[] travelAgents = new TravelAgent[5];

                // Create Hotel thread
                Console.WriteLine("Creating hotel thread");
                Thread hotelThread = new Thread(new ThreadStart(hotel.hotelFun));
                hotelThread.Start();

                // Subscribe to events
                Console.WriteLine("Price cut event has been subscribed");
                Console.WriteLine("Order creation event has been subscribed");
                Console.WriteLine("Order process event has been subscribed");

                // Create TravelAgent threads
                travelAgentThreads = new Thread[5];
                for (int i = 0; i < 5; i++)
                {
                    travelAgents[i] = new TravelAgent(i + 1);
                    Hotel.PriceCut += new PriceCutEvent(travelAgents[i].agentOrder);
                    TravelAgent.orderCreation += new OrderCreationEvent(hotel.takeOrder);
                    OrderProcessing.OrderProcess += new OrderProcessEvent(travelAgents[i].orderProcessConfirm);

                    Console.WriteLine("Creating travel agent thread {0}", (i + 1));
                    travelAgentThreads[i] = new Thread(travelAgents[i].agentFun);
                    travelAgentThreads[i].Name = (i + 1).ToString();
                    Console.WriteLine("Starting travel agent now");
                    travelAgentThreads[i].Start();
                }

                // Wait for Hotel thread to finish
                hotelThread.Join();

                // Set hotelThreadRunning to false, so agents stop
                hotelThreadRunning = false;

                // Wait for each TravelAgent thread to finish
                foreach (Thread agentThread in travelAgentThreads)
                {
                    agentThread.Join();
                }

                Console.WriteLine("All threads have completed execution.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }
        }
    }

    public class MultiCellBuffer
    {
        private const int bufferSize = 3; // Buffer size
        int usedCells;
        private Order[] multiCells;
        public static Semaphore getSemaph;
        public static Semaphore setSemaph;

        public MultiCellBuffer() // Constructor
        {
            multiCells = new Order[bufferSize];
            usedCells = 0;
            getSemaph = new Semaphore(0, bufferSize);
            setSemaph = new Semaphore(bufferSize, bufferSize);
        }

        // Method to write data into the buffer
        public void SetOneCell(Order data)
        {
            setSemaph.WaitOne(); // Wait for available write slot
            lock (this) // Lock the buffer to write data
            {
                for (int i = 0; i < bufferSize; i++)
                {
                    if (multiCells[i] == null)
                    {
                        multiCells[i] = data;
                        usedCells++;
                        Console.WriteLine("Setting in buffer cell {0}", i);
                        break;
                    }
                }
            }
            Console.WriteLine("Exit setting in buffer");
            getSemaph.Release(); // Signal that a read slot is available
        }

        // Method to read data from the buffer
        public Order GetOneCell()
        {
            Order order = null;
            getSemaph.WaitOne(); // Wait for available read slot
            Monitor.Enter(this); // Enter monitor to read data
            try
            {
                for (int i = 0; i < bufferSize; i++)
                {
                    if (multiCells[i] != null)
                    {
                        order = multiCells[i];
                        multiCells[i] = null;
                        usedCells--;
                        Console.WriteLine("Reading from buffer cell {0}", i);
                        break;
                    }
                }
            }
            finally
            {
                Monitor.Exit(this); // Exit monitor after reading
            }
            Console.WriteLine("Exit reading buffer");
            setSemaph.Release(); // Signal that a write slot is available
            return order;
        }
    }

    public class Order
    {
        private string senderId; // Identity of the sender
        private long cardNo; // Credit card number
        private string receiverId; // Optional receiver identity
        private double unitPrice; // Unit price of the room
        private int quantity; // Quantity of rooms ordered

        public Order(string senderId, long cardNo, double unitPrice, int quantity, string receiverId = null)
        {
            this.senderId = senderId;
            this.cardNo = cardNo;
            this.unitPrice = unitPrice;
            this.quantity = quantity;
            this.receiverId = receiverId;
        }

        // Public methods to set and get private data members
        public string getSenderId() { lock (this) { return senderId; } }
        public void setSenderId(string value) { lock (this) { senderId = value; } }

        public long getCardNo() { lock (this) { return cardNo; } }
        public void setCardNo(long value) { lock (this) { cardNo = value; } }

        public string getReceiverId() { lock (this) { return receiverId; } }
        public void setReceiverId(string value) { lock (this) { receiverId = value; } }

        public double getUnitPrice() { lock (this) { return unitPrice; } }
        public void setUnitPrice(double value) { lock (this) { unitPrice = value; } }

        public int getQuantity() { lock (this) { return quantity; } }
        public void setQuantity(int value) { lock (this) { quantity = value; } }
    }

    public class OrderProcessing
    {
        public static event OrderProcessEvent OrderProcess;

        // Method to check validity of credit card number
        public static bool creditCardCheck(long creditCardNumber)
        {
            // Valid credit card numbers are between 5000 and 7000
            return creditCardNumber >= 5000 && creditCardNumber <= 7000;
        }

        // Method to calculate the total charge for the order
        public static double calculateCharge(double unitPrice, int quantity)
        {
            // Tax rate between 8% and 12%
            double taxRate = new Random().Next(8, 13) / 100.0;
            // Location charge between $20 and $80
            double locationCharge = new Random().Next(20, 81);
            // Calculate total amount
            double tax = unitPrice * quantity * taxRate;
            return (unitPrice * quantity) + tax + locationCharge;
        }

        // Method to process the order
        public static void ProcessOrder(Order order)
        {
            try
            {
                if (creditCardCheck(order.getCardNo()))
                {
                    double totalAmount = calculateCharge(order.getUnitPrice(), order.getQuantity());
                    Console.WriteLine($"Travel Agent {order.getSenderId()}'s order is confirmed. The amount to be charged is ${totalAmount}");
                    OrderProcess?.Invoke(order, totalAmount);
                }
                else
                {
                    Console.WriteLine($"Order from {order.getSenderId()} failed due to invalid credit card number.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while processing the order: {ex.Message}");
            }
        }
    }

    public class TravelAgent
    {
        private double currentPrice; // Current room price
        private readonly Random rand = new Random(); // Random generator
        private int agentId; // Travel agent identifier
        public static event OrderCreationEvent orderCreation;

        public TravelAgent(int id)
        {
            agentId = id;
        }

        // Thread function for travel agent
        public void agentFun()
        {
            double previousPrice = double.MaxValue; // Keep track of previous price
            while (MainClass.hotelThreadRunning)
            {
                // Wait for price cut to determine whether to place an order
                if (currentPrice > 0 && currentPrice < previousPrice) // Order if new price is lower than previous
                {
                    createOrder(Thread.CurrentThread.Name);
                    previousPrice = currentPrice; // Update previous price
                    currentPrice = 0; // Reset after placing order
                }
                Thread.Sleep(rand.Next(500, 1000));
            }
        }

        // Method to confirm order processing
        public void orderProcessConfirm(Order order, double orderAmount)
        {
            Console.WriteLine($"Travel Agent {agentId}'s order is confirmed. The amount to be charged is ${orderAmount}");
        }

        // Method to create an order
        private void createOrder(string senderId)
        {
            try
            {
                Console.WriteLine("Inside create order");
                long cardNo = rand.Next(5000, 7001);
                double unitPrice = currentPrice; // Use current price as unit price
                int quantity = rand.Next(1, 6);
                string receiverId = "Hotel1"; // Example receiver ID
                Order order = new Order(senderId, cardNo, unitPrice, quantity, receiverId);
                MainClass.buffer.SetOneCell(order);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while creating the order: {ex.Message}");
            }
        }

        // Event handler for receiving new room price
        public void agentOrder(double roomPrice, Thread travelAgent)
        {
            // Set new price for evaluation
            Console.WriteLine($"Travel Agent {agentId} received new price: {roomPrice}");
            currentPrice = roomPrice;
        }
    }

    public class Hotel
    {
        static double currentRoomPrice = 100; // Current room price
        static int priceCutCount = 0; // Counter for price cuts
        public static event PriceCutEvent PriceCut;
        private static readonly Random rand = new Random(); // Random generator

        // Thread function for hotel to manage room prices
        public void hotelFun()
        {
            try
            {
                while (priceCutCount < 10)
                {
                    double newPrice = pricingModel();
                    if (newPrice < currentRoomPrice)
                    {
                        Console.WriteLine("Updating the price and calling price cut event");
                        PriceCut?.Invoke(newPrice, Thread.CurrentThread);
                        priceCutCount++;
                    }
                    updatePrice(newPrice);
                    Thread.Sleep(rand.Next(500, 1000));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred in the hotel thread: {ex.Message}");
            }
        }

        // Method to determine new room price
        public double pricingModel()
        {
            double priceChange = rand.Next(-20, 21); // Generate a value between -20 and 20 to simulate price fluctuation
            double newPrice = currentRoomPrice + priceChange;
            if (newPrice < 80) newPrice = 80;
            if (newPrice > 160) newPrice = 160;
            return newPrice;
        }

        // Method to update current room price
        public void updatePrice(double newRoomPrice)
        {
            currentRoomPrice = newRoomPrice;
            Console.WriteLine($"New price is {currentRoomPrice}");
        }

        // Method to take an order from the buffer
        public void takeOrder()
        {
            try
            {
                Console.WriteLine("Incoming order for room with price {0}", currentRoomPrice);
                Order order = MainClass.buffer.GetOneCell();
                if (order != null)
                {
                    Thread orderProcessingThread = new Thread(() => OrderProcessing.ProcessOrder(order));
                    orderProcessingThread.Start();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while taking the order: {ex.Message}");
            }
        }
    }
}
