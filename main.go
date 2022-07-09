package main

import (
	"cloud.google.com/go/firestore"
	"context"
	firebase "firebase.google.com/go"
	tgbotapi "github.com/go-telegram-bot-api/telegram-bot-api/v5"
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
}

func isUserRegistered(update tgbotapi.Update) bool {
	_, err := client.Collection("users").Doc(update.SentFrom().UserName).Get(context.Background())

	return err == nil
}

func addToPendingCheckIn(update tgbotapi.Update) {
	pendingCheckIn[update.SentFrom().UserName] = true
	msg := tgbotapi.NewMessage(update.Message.Chat.ID, "Great, send your photo!")
	bot.Send(msg)
}

func tryCheckIn(update tgbotapi.Update) bool {
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
	m := make(map[string]string)
	m["lastCheckIn"] = strconv.FormatInt(time.Now().Unix(), 10)

	result, err := client.Collection("users").Doc(update.SentFrom().UserName).Set(context.Background(), m)

	if err != nil {
		log.Fatalln(err)
	}

	log.Println(result)
	msg := tgbotapi.NewMessage(update.Message.Chat.ID, "Ok, you're checked in.")
	bot.Send(msg)
}
