package main

import (
	"context"
	"fmt"
	"io/ioutil"
	"log"
	"math/rand"
	"strings"
	"time"

	"cloud.google.com/go/firestore"
	firebase "firebase.google.com/go"
	tgbotapi "github.com/go-telegram-bot-api/telegram-bot-api/v5"
	"google.golang.org/api/iterator"
	"google.golang.org/api/option"
)

var client *firestore.Client
var pendingCheckIn map[string]bool
var bot *tgbotapi.BotAPI

func main() {
	// CONFIG IF DEPLOYED
	//conf := &firebase.Config{ProjectID: "gainz-c5ddd"}
	//app, err := firebase.NewApp(context.Background(), conf)

	// CONFIG IF RUNNING LOCAL
	sa := option.WithCredentialsFile("./gainz-c5ddd-firebase-adminsdk-hgnr0-37d5263c09.json")
	app, err := firebase.NewApp(context.Background(), nil, sa)
	if err != nil {
		log.Panic(err)
	}

	client, err = app.Firestore(context.Background())
	if err != nil {
		log.Fatalln(err)
	}

	telegramKey, err := ioutil.ReadFile("telegram_key.txt")

	if err != nil {
		log.Fatal(err)
	}

	bot, err = tgbotapi.NewBotAPI(string(telegramKey))
	if err != nil {
		log.Panic(err)
	}

	d, _ := time.ParseDuration("10m")
	go pollCheckInTimes(d)

	bot.Debug = true

	log.Printf("Authorized on account %s", bot.Self.UserName)

	u := tgbotapi.NewUpdate(0)
	u.Timeout = 60

	updates := bot.GetUpdatesChan(u)

	pendingCheckIn = make(map[string]bool)

	for update := range updates {
		if update.Message != nil { // If we got a message

			if update.Message.Command() == "register" {
				register(update)
			} else if update.Message.Command() == "checkin" {
				addToPendingCheckIn(update)
			} else if update.Message.Command() == "stats" {
				getStats(update)
			}

			// User might be trying to send their check in photo/video
			if update.Message.Photo != nil || update.Message.Document != nil || update.Message.Video != nil {
				tryCheckIn(update)
			}
		}
	}
}

func register(update tgbotapi.Update) {
	if isUserRegistered(update) {
		log.Print("USER IS REGISTERED")
		msg := tgbotapi.NewMessage(update.Message.Chat.ID, "You're already registered.")
		bot.Send(msg)
		return
	} else {
		log.Print("USER IS NOT REGISTERED")
	}

	m := make(map[string]interface{})
	m["chatID"] = update.FromChat().ID
	m["telegramID"] = update.SentFrom().ID
	m["lastCheckIn"] = time.Now().Unix()
	m["hasBeenWarned"] = false
	m["totalCheckIns"] = 0

	result, err := client.Collection("users").Doc(update.SentFrom().UserName).Set(context.Background(), m)

	if err != nil {
		log.Fatalln(err)
	}

	log.Println(result)

	msg := tgbotapi.NewMessage(update.Message.Chat.ID, "✅ You're now registered to this chat.")
	bot.Send(msg)
}

func isUserRegistered(update tgbotapi.Update) bool {
	_, err := client.Collection("users").Doc(update.SentFrom().UserName).Get(context.Background())

	return err == nil
}

func addToPendingCheckIn(update tgbotapi.Update) {
	if !isUserRegistered(update) {
		msg := tgbotapi.NewMessage(update.Message.Chat.ID, "🤔 Use /register to register your account first!")
		bot.Send(msg)
		return
	}

	pendingCheckIn[update.SentFrom().UserName] = true
	msg := tgbotapi.NewMessage(update.Message.Chat.ID, "⭐ Great, send your photo or video!")
	bot.Send(msg)
}

func tryCheckIn(update tgbotapi.Update) bool {
	log.Print("CHECKING ATTEMPT")

	if !isUserRegistered(update) {
		msg := tgbotapi.NewMessage(update.Message.Chat.ID, "🤔 Use /register to register your account first!")
		bot.Send(msg)
		return false
	}

	if value, exist := pendingCheckIn[update.SentFrom().UserName]; exist {
		if value == true {
			log.Print("User is waiting to check in!")
			delete(pendingCheckIn, update.SentFrom().UserName)
			writeCheckInTime(update)
			return true
		}
	} else {
		log.Print("User wasnt waiting...")
	}

	return false
}

func writeCheckInTime(update tgbotapi.Update) {
	dc := client.Collection("users").Doc(update.SentFrom().UserName)
	result, err := dc.Update(context.Background(), []firestore.Update{
		{Path: "lastCheckIn", Value: time.Now().Unix()},
		{Path: "totalCheckIns", Value: firestore.Increment(1)},
		{Path: "hasBeenWarned", Value: false},
	})
	if err != nil {
		return
	}

	log.Println(result)

	fileName := fmt.Sprintf("./checkinmessages/%s.txt", update.SentFrom().UserName)
	fileContents, err := ioutil.ReadFile(fileName)

	checkInMessages := strings.Split(string(fileContents), ",")
	messageIndex := 0

	if err != nil { // No custom messages found for this user
		checkInMessages = []string{"Awesome, you're checked in!"}
		log.Println("No custom message found. Using default.")
	} else {
		rand.Seed(time.Now().Unix())
		messageIndex = rand.Int() % len(checkInMessages)
		log.Printf("Custom message found %d", messageIndex)
	}

	msg := tgbotapi.NewMessage(update.Message.Chat.ID, fmt.Sprintf("🎉 %s You've checked in %s times.", checkInMessages[messageIndex], getUserCheckInCount(update)))

	bot.Send(msg)
}

func getStats(update tgbotapi.Update) {
	chatID := update.FromChat().ID
	iter := client.Collection("users").OrderBy("totalCheckIns", firestore.Desc).Where("chatID", "==", chatID).Limit(10).Documents(context.Background())

	stats := make(map[string]string)

	log.Println("Checking stats for chatID " + string(chatID))

	for {
		doc, err := iter.Next()
		if err == iterator.Done {
			break
		}
		if err != nil {
			log.Println(err)
			return
		}

		stats[doc.Ref.ID] = fmt.Sprint(doc.Data()["totalCheckIns"])
	}

	msg := "No stats yet."

	if len(stats) > 0 {
		msg = "💯 Check-In Stats\n\n"
	}

	for key, element := range stats {
		msg += key + ": " + element + "\n"
	}

	bot.Send(tgbotapi.NewMessage(update.Message.Chat.ID, msg))
}

func getUserCheckInCount(update tgbotapi.Update) string {
	user, err := client.Collection("users").Doc(update.SentFrom().UserName).Get(context.Background())
	if err != nil {
		return "null"
	}

	log.Println(user.Data()["totalCheckIns"])
	return fmt.Sprint(user.Data()["totalCheckIns"])
}

func pollCheckInTimes(d time.Duration) {
	for true {
		log.Print("Polling for kicks/warnings...")

		oneWeekAgo := time.Now().Add(-168 * time.Hour).Unix()
		sixDaysAgo := time.Now().Add(-144 * time.Hour).Unix()

		iter := client.Collection("users").Where("lastCheckIn", "<=", sixDaysAgo).Documents(context.Background())

		for {
			doc, err := iter.Next()
			if err == iterator.Done {
				break
			}
			if err != nil {
				return
			}

			convertedInt, ok := doc.Data()["lastCheckIn"].(int64)

			if !ok {
				panic("Bad int convert")
				return
			}

			hasBeenWarned := false

			if doc.Data()["hasBeenWarned"] != nil {
				hasBeenWarned = doc.Data()["hasBeenWarned"].(bool)
			}

			chatID := doc.Data()["chatID"].(int64)

			if convertedInt > oneWeekAgo && convertedInt < sixDaysAgo && !hasBeenWarned {
				setUserAsWarned(doc.Ref.ID)
				bot.Send(tgbotapi.NewMessage(chatID, "⚠️ @"+doc.Ref.ID+", you have 24 hours to submit a check-in before you are kicked!"))
			} else if convertedInt <= oneWeekAgo {
				bot.Send(tgbotapi.NewMessage(chatID, "💣 @"+doc.Ref.ID+", you've been kicked due to not sending a gainz check-in in 7 days. All data has been deleted."))
				bot.Send(tgbotapi.KickChatMemberConfig{
					ChatMemberConfig: tgbotapi.ChatMemberConfig{
						ChatID: chatID,
						UserID: doc.Data()["telegramID"].(int64),
					},
					UntilDate:      0,
					RevokeMessages: false,
				})

				client.Collection("users").Doc(doc.Ref.ID).Delete(context.Background())
			}
		}
		time.Sleep(d)
	}
}

func setUserAsWarned(docID string) {
	dc := client.Collection("users").Doc(docID)
	result, err := dc.Update(context.Background(), []firestore.Update{
		{Path: "hasBeenWarned", Value: true},
	})
	if err != nil {
		return
	}

	log.Println(result)
}
