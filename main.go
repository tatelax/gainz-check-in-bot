package main

import (
	"cloud.google.com/go/firestore"
	"context"
	firebase "firebase.google.com/go"
	"fmt"
	tgbotapi "github.com/go-telegram-bot-api/telegram-bot-api/v5"
	"google.golang.org/api/iterator"
	"google.golang.org/api/option"
	"log"
	"time"
)

var client *firestore.Client
var pendingCheckIn map[string]bool
var bot *tgbotapi.BotAPI

func main() {
	sa := option.WithCredentialsFile("./gainz-c5ddd-firebase-adminsdk-hgnr0-37d5263c09.json")
	app, err := firebase.NewApp(context.Background(), nil, sa)
	if err != nil {
		log.Panic(err)
	}

	client, err = app.Firestore(context.Background())
	if err != nil {
		log.Fatalln(err)
	}

	bot, err = tgbotapi.NewBotAPI("5408693130:AAFVr_CKP8btv17nGzHoacGsVMvC5KJb8b4")
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

			// User might be trying to send their check in photo
			if update.Message.Photo != nil || update.Message.Document != nil {
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
	m["lastCheckIn"] = time.Now().Unix()
	m["chatID"] = update.FromChat().ID
	m["telegramID"] = update.SentFrom().ID

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
	msg := tgbotapi.NewMessage(update.Message.Chat.ID, "⭐ Great, send your photo!")
	bot.Send(msg)
}

func tryCheckIn(update tgbotapi.Update) bool {
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
	})
	if err != nil {
		return
	}

	log.Println(result)
	msg := tgbotapi.NewMessage(update.Message.Chat.ID, fmt.Sprintf("🎉 Awesome, you're checked in! You've checked in %s times.", getUserCheckInCount(update)))
	bot.Send(msg)
}

func getStats(update tgbotapi.Update) {
	chatID := update.FromChat().ID
	iter := client.Collection("users").OrderBy("totalCheckIns", firestore.Asc).Where("chatID", "==", chatID).Limit(10).Documents(context.Background())

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
		msg = "💯 Check-In Leaderboard\n\n"
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

			chatID := doc.Data()["chatID"].(int64)

			log.Println(chatID)

			if convertedInt > oneWeekAgo && convertedInt < sixDaysAgo {
				bot.Send(tgbotapi.NewMessage(chatID, "⚠️ You have 24 hours to submit a check-in before you are kicked!"))
			} else if convertedInt <= oneWeekAgo {
				bot.Send(tgbotapi.NewMessage(chatID, "💣 You've been kicked due to not sending a gainz check-in in 7 days. All data has been deleted."))
				bot.Send(tgbotapi.KickChatMemberConfig{
					ChatMemberConfig: tgbotapi.ChatMemberConfig{
						ChatID:             chatID,
						SuperGroupUsername: "",
						ChannelUsername:    "",
						UserID:             doc.Data()["telegramID"].(int64),
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
