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
	"strconv"
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

	m := make(map[string]string)
	m["lastCheckIn"] = strconv.FormatInt(time.Now().Unix(), 10)

	result, err := client.Collection("users").Doc(update.SentFrom().UserName).Set(context.Background(), m)

	if err != nil {
		log.Fatalln(err)
	}

	log.Println(result)

	msg := tgbotapi.NewMessage(update.Message.Chat.ID, "You're now registered.")
	bot.Send(msg)
}

func isUserRegistered(update tgbotapi.Update) bool {
	_, err := client.Collection("users").Doc(update.SentFrom().UserName).Get(context.Background())

	return err == nil
}

func addToPendingCheckIn(update tgbotapi.Update) {
	if !isUserRegistered(update) {
		msg := tgbotapi.NewMessage(update.Message.Chat.ID, "Use /register to register your account first!")
		bot.Send(msg)
		return
	}

	pendingCheckIn[update.SentFrom().UserName] = true
	msg := tgbotapi.NewMessage(update.Message.Chat.ID, "Great, send your photo!")
	bot.Send(msg)
}

func tryCheckIn(update tgbotapi.Update) bool {
	if !isUserRegistered(update) {
		msg := tgbotapi.NewMessage(update.Message.Chat.ID, "Use /register to register your account first!")
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
	msg := tgbotapi.NewMessage(update.Message.Chat.ID, fmt.Sprintf("Awesome, you're checked in! You've checked in %s times.", getUserCheckInCount(update)))
	bot.Send(msg)
}

func getStats(update tgbotapi.Update) {
	iter := client.Collection("users").OrderBy("totalCheckIns", firestore.Asc).Limit(10).Documents(context.Background())

	stats := make(map[string]string)

	for {
		doc, err := iter.Next()
		if err == iterator.Done {
			break
		}
		if err != nil {
			return
		}

		stats[doc.Ref.ID] = fmt.Sprint(doc.Data()["totalCheckIns"])
	}

	msg := "Check-In Leaderboard\n\n"

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
