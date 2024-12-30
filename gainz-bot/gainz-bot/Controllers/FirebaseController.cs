using Google.Cloud.Firestore;

namespace gainz_bot;

public class FirebaseController
{
    public FirestoreDb? db { get; private set; }

    private static readonly Lazy<FirebaseController> lazy = new(() => new FirebaseController());

    public static FirebaseController Instance => lazy.Value;

    public async Task Run(CancellationToken cancellationToken)
    {
        try
        {
            db = await FirestoreDb.CreateAsync("gainz-c5ddd");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }
}