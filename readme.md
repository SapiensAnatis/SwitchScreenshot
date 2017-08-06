# Discord/Twitter bot to save users 3 clicks

These bots work together to detect screenshots posted by 'subscribed' users and PM the pictures to the users on Discord.
It was never intended to be publically used, so it's got hardcoded paths and a lot of assumptions about SQL being in place (see init.sql for setup)

Commands:

- DM the bot register <twitterusername> to subscribe to a twitter username and receive all single-image tweets with #NintendoSwitch in them to your Discord inbox.