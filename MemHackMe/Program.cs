
int prevValue = 0;
int hackMe = 0;

while(true)
{
    Console.Clear();
    Console.WriteLine("___________VALUES___________");
    Console.WriteLine($"Previous value: {prevValue}");
    Console.WriteLine($"HackMe Value: {hackMe}");
    Console.WriteLine("____________MENU____________");
    Console.WriteLine("1. Add 1 to the value");
    Console.WriteLine("2. Do nothing");

    Console.WriteLine("Enter the option: ");
    bool isOption = int.TryParse(Console.ReadLine(), out int option);
    if (!isOption)
    {
        Console.WriteLine("Invalid option");
        continue;
    }

    switch (option)
    {
        case 1:
            prevValue = hackMe;
            hackMe++;
            break;
        case 2:
            break;
        default:
            Console.WriteLine("Invalid option");
            continue;
    }
}
