-- It uses MySql so the user has to create the DB and stuff themselves. This is a primer to remember what the parameters were
CREATE DATABASE IF NOT EXISTS SwitchScreenshotsDB;

CREATE USER 'SwitchScreenshotsBot'@'localhost' IDENTIFIED BY 'GOOD password';
GRANT ALL ON SwitchScreenshotsDB.* TO 'SwitchScreenshotsBot'@'localhost';
USE SwitchScreenshotsDB;

CREATE TABLE IF NOT EXISTS DiscordUsers(Id BIGINT UNSIGNED NOT NULL PRIMARY KEY);
CREATE TABLE IF NOT EXISTS TwitterUsers(Id BIGINT UNSIGNED NOT NULL PRIMARY KEY);
CREATE TABLE IF NOT EXISTS DiscordTwitterUsers(DiscordId BIGINT UNSIGNED NOT NULL, TwitterId BIGINT UNSIGNED NOT NULL);