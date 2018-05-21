from flask_restful import Resource
from flask import request, jsonify
from flask_jwt_extended import create_access_token
from master import app, ctx
import datetime
import urllib.request
import csv
from io import StringIO

class Localization(Resource):
    def list(self):
        response = urllib.request.urlopen('https://docs.google.com/spreadsheets/d/e/2PACX-1vRQjCqPvd0Xqcn86WqpFqp_lx4KKpel9O4OV13NycmV8rmqycorgJQm-8qXMfw37QJHun3pqVZFUKG-/pub?gid=0&single=true&output=csv')
        data = response.read().decode('utf-8')

        localization = []
        csv_data = csv.DictReader(StringIO(data))

        for language in csv_data.fieldnames[1:]:
            localization.append({
                    'LocalizationName' : language,
                     'LocalizationIndex' : {
                     'Set' : {}
                     }
                })

        for row in csv_data:
            localization_string = row['STRING']
            count = 0
            for language in  csv_data.fieldnames[1:]:
                localization[count]['LocalizationIndex']['Set'][localization_string] = row[language]
                count += 1

        return localization, 200

    def get(self, language_tag=None):
        response = urllib.request.urlopen('https://docs.google.com/spreadsheets/d/e/2PACX-1vRQjCqPvd0Xqcn86WqpFqp_lx4KKpel9O4OV13NycmV8rmqycorgJQm-8qXMfw37QJHun3pqVZFUKG-/pub?gid=0&single=true&output=csv')
        data = response.read().decode('utf-8')

        csv_data = csv.DictReader(StringIO(data))
        

        if language_tag != None:
           valid_language_tag = next((l for l in csv_data.fieldnames[1:] if l == language_tag), None)
           if valid_language_tag is None:
               valid_language_tag = next((l for l in csv_data.fieldnames[1:] if l.startswith(language_tag[:2])), None)
           if valid_language_tag is None:
               valid_language_tag = 'en-US'   
           localization = {
                  'LocalizationName' : valid_language_tag,
                  'LocalizationIndex' : {
                  'Set' : {}
                  }
                }
           for row in csv_data:
               localization_string = row['STRING']
               localization['LocalizationIndex']['Set'][localization_string] = row[valid_language_tag]
           return localization, 200
        else:
           return self.list()[0][0], 200
