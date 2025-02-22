using MemHackLib;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text;

namespace MemHack;

public class Program
{
    private static byte[] buffer = [];
    private static List<nint> foundAddresses = [];

    private static void Main(string[] args)
    {
        IMemHack memHack = IMemHack.Create();

        List<(string title, uint processId)> windows = memHack.GetAllProcesses();
        int j = 0;
        foreach ((string title, uint processId) window in windows)
        {
            Console.WriteLine($"{j}. {window.title} - {window.processId}");
            j++;
        }

        Console.Write("Enter the window number: ");
        bool isWindowNumber = int.TryParse(Console.ReadLine(), out int windowNumber);

        if (!isWindowNumber)
        {
            Console.WriteLine("Invalid window number");
            return;
        }

        if (windowNumber < 0 || windowNumber >= windows.Count)
        {
            Console.WriteLine("Invalid window number");
            return;
        }

        (string title, uint processId) = windows[windowNumber];

        Console.Write("Enter the value: ");
        bool isValue = long.TryParse(Console.ReadLine(), out long desiredValue);

        if (!isValue)
        {
            Console.WriteLine("Invalid value");
            return;
        }

        Console.WriteLine("Searching for addresses...");
        foundAddresses = memHack.MemorySearch(processId, desiredValue);

        int k = 0;
        while (true)
        {
            Console.Clear();
            Console.WriteLine("____________INFO____________");
            Console.WriteLine($"Window title: {title}");
            Console.WriteLine($"Process ID: {processId}");
            Console.WriteLine($"Addresses found: {foundAddresses.Count}");
            Console.WriteLine("____________MENU____________");
            Console.WriteLine("1. Print addresses");
            Console.WriteLine("2. Filter for new value");
            Console.WriteLine("3. Change address value");
            Console.WriteLine("4. Change user given address");
            Console.WriteLine("5. Restart");
            Console.WriteLine("6. Exit");

            Console.Write("Enter the option: ");
            string option = Console.ReadLine();
            if (option != null)
                switch (option)
                {
                    case "1":
                        k = 0;
                        foreach (nint foundAddress in foundAddresses)
                        {
                            long value = Utils.BufferConvert(buffer, 0, memHack.ValueType);
                            Console.WriteLine($"{k}. 0x{foundAddress.ToInt64():X} - {value}");
                            k++;
                        }

                        Console.WriteLine("Press any key to continue...");
                        Console.ReadKey();
                        break;
                    case "2":
                        Console.Write("Enter the new value: ");
                        isValue = long.TryParse(Console.ReadLine(), out long newValue);

                        if (!isValue)
                        {
                            Console.WriteLine("Invalid value");
                            break;
                        }

                        foundAddresses = memHack.FilterPointers(processId, foundAddresses, newValue);

                        break;
                    case "3":
                        int index = 0;
                        if (foundAddresses.Count > 1)
                        {
                            Console.WriteLine("Enter the address index: ");
                            isValue = int.TryParse(Console.ReadLine(), out index);

                            if (!isValue || index < 0 || index >= foundAddresses.Count)
                            {
                                Console.WriteLine("Invalid index");
                                break;
                            }
                        }

                        Console.WriteLine("Enter the new value: ");
                        isValue = int.TryParse(Console.ReadLine(), out int newAddressValue);

                        if (!isValue)
                        {
                            Console.WriteLine("Invalid value");
                            break;
                        }

                        memHack.WriteAddressValue(processId, foundAddresses[index], newAddressValue);

                        Console.WriteLine("Press any key to continue...");
                        Console.ReadKey();

                        break;
                    case "4":
                        Console.WriteLine("Enter the address to read: ");
                        string addressString = Console.ReadLine();
                        bool isAddress = long.TryParse(addressString, out long addressValue);
                        if (!isAddress)
                        {
                            Console.WriteLine("Invalid address");
                            return;
                        }

                        // read new address value
                        Console.WriteLine("Enter the new address value: ");
                        string newAddressString = Console.ReadLine();
                        bool isNewAddress = long.TryParse(newAddressString, out long newaddrvalue);
                        if (!isNewAddress)
                        {
                            Console.WriteLine("Invalid new address value");
                            return;
                        }

                        memHack.WriteAddressValue(processId, (nint)addressValue, newaddrvalue);
                        Console.ReadLine();

                        break;
                    case "5":
                        buffer = [];
                        foundAddresses = [];
                        Main(args);
                        break;
                    case "6":
                        return;
                    default:
                        Console.WriteLine("Invalid option");
                        break;
                }
        }
    }
}