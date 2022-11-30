using Google.Cloud.Firestore;

namespace gainz_bot;

public class FirebaseController
{
    public FirestoreDb? db { get; private set; }

    private static readonly Lazy<FirebaseController> lazy = new(() => new FirebaseController());

    public static FirebaseController Instance => lazy.Value;
    
    public async Task Run()
    {
        db = await FirestoreDb.CreateAsync("gainz-c5ddd");
    }
}