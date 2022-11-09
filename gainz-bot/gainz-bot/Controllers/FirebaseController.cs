using Google.Cloud.Firestore;

namespace gainz_bot;

public class FirebaseController
{
    public async Task Run()
    {

        var db = FirestoreDb.Create("gainz-dev");

        var collection = db.Collection("users");
        var doc = await collection.GetSnapshotAsync();

        var idk = collection.GetSnapshotAsync();

        foreach (var documentSnapshot in doc.Documents)
        {
            Console.WriteLine(documentSnapshot.Id);
        }

    }
}