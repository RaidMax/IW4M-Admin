import requests
import time
import json
import collections
import os

# the following classes model the discord webhook api parameters
class WebhookAuthor():
    def __init__(self, name=None, url=None, icon_url=None):
        if name:
            self.name = name
        if url:
            self.url = url
        if icon_url:
            self.icon_url = icon_url

class WebhookField():
    def __init__(self, name=None, value=None, inline=False):
        if name:
            self.name = name
        if value:
            self.value = value
        if inline:
            self.inline = inline

class WebhookEmbed():
    def __init__(self):
        self.author = ''
        self.title = ''
        self.url = ''
        self.description = ''
        self.color = 0
        self.fields = []
        self.thumbnail = {}

class WebhookParams():
    def __init__(self, username=None, avatar_url=None, content=None):
        self.username = ''
        self.avatar_url = ''
        self.content = ''
        self.embeds = []

    # quick way to convert all the objects to a nice json object
    def to_json(self):
        return json.dumps(self, default=lambda o: o.__dict__, sort_keys=True)

# gets the relative link to a user's profile
def get_client_profile(profile_id):
    return u'{}/Client/ProfileAsync/{}'.format(base_url, profile_id)

def get_client_profile_markdown(client_name, profile_id):
    return u'[{}]({})'.format(client_name, get_client_profile(profile_id))

#todo: exception handling for opening the file
if os.getenv("DEBUG"):
    config_file_name = 'config.dev.json'
else:
    config_file_name = 'config.json'

with open(config_file_name) as json_config_file:
    json_config = json.load(json_config_file)

# this should be an URL to an IP or FQN to an IW4MAdmin instance
# ie http://127.0.0.1 or http://IW4MAdmin.com
base_url = json_config['IW4MAdminUrl']
end_point = '/api/event'
request_url = base_url + end_point
# this should be the full discord webhook url
# ie https://discordapp.com/api/webhooks/<id>/<token>
discord_webhook_notification_url = json_config['DiscordWebhookNotificationUrl']
discord_webhook_information_url = json_config['DiscordWebhookInformationUrl']
# this should be the numerical id of the discord group
# 12345678912345678
notify_role_ids = json_config['NotifyRoleIds']

def get_new_events():
    events = []
    response = requests.get(request_url)
    data = response.json()
    should_notify = False
    
    for event in data:
        # commonly used event info items
        event_type = event['eventType']['name']
        server_name = event['ownerEntity']['name']

        if event['originEntity']:
            origin_client_name = event['originEntity']['name']
            origin_client_id = int(event['originEntity']['id'])

        if event['targetEntity']:
            target_client_name = event['targetEntity']['name'] or ''
            target_client_id = int(event['targetEntity']['id']) or 0

        webhook_item = WebhookParams()
        webhook_item_embed = WebhookEmbed()

        #todo: the following don't need to be generated every time, as it says the same
        webhook_item.username = 'IW4MAdmin'
        webhook_item.avatar_url = 'https://raidmax.org/IW4MAdmin/img/iw4adminicon-3.png'
        webhook_item_embed.color = 31436
        webhook_item_embed.url = base_url
        webhook_item_embed.thumbnail = { 'url' : 'https://raidmax.org/IW4MAdmin/img/iw4adminicon-3.png' }
        webhook_item.embeds.append(webhook_item_embed)

        # the server should be visible on all event types
        server_field = WebhookField('Server', server_name)
        webhook_item_embed.fields.append(server_field)

        role_ids_string = ''
        for id in notify_role_ids:
            role_ids_string += '\r\n<@&{}>\r\n'.format(id)
    
        if event_type == 'Report':
            report_reason = event['extraInfo']

            report_reason_field = WebhookField('Reason', report_reason)
            reported_by_field = WebhookField('By', get_client_profile_markdown(origin_client_name, origin_client_id))
            reported_field = WebhookField('Reported Player',get_client_profile_markdown(target_client_name, target_client_id))

            # add each fields to the embed
            webhook_item_embed.title = 'Player Reported'
            webhook_item_embed.fields.append(reported_field)
            webhook_item_embed.fields.append(reported_by_field)
            webhook_item_embed.fields.append(report_reason_field)

            should_notify = True

        elif event_type == 'Ban':
            ban_reason = event['extraInfo']
            ban_reason_field = WebhookField('Reason', ban_reason)
            banned_by_field = WebhookField('By',  get_client_profile_markdown(origin_client_name, origin_client_id))
            banned_field = WebhookField('Banned Player', get_client_profile_markdown(target_client_name, target_client_id))

            # add each fields to the embed
            webhook_item_embed.title = 'Player Banned'
            webhook_item_embed.fields.append(banned_field)
            webhook_item_embed.fields.append(banned_by_field)
            webhook_item_embed.fields.append(ban_reason_field)

            should_notify = True

        elif event_type == 'Connect':
            connected_field = WebhookField('Connected Player', get_client_profile_markdown(origin_client_name, origin_client_id))
            webhook_item_embed.title = 'Player Connected'
            webhook_item_embed.fields.append(connected_field)

        elif event_type == 'Disconnect':
            disconnected_field = WebhookField('Disconnected Player', get_client_profile_markdown(origin_client_name, origin_client_id))
            webhook_item_embed.title = 'Player Disconnected'
            webhook_item_embed.fields.append(disconnected_field)

        elif event_type == 'Say':
            say_client_field = WebhookField('Player', get_client_profile_markdown(origin_client_name, origin_client_id))
            message_field = WebhookField('Message', event['extraInfo'])

            webhook_item_embed.title = 'Message From Player'
            webhook_item_embed.fields.append(say_client_field)
            webhook_item_embed.fields.append(message_field)

        #if event_type == 'ScriptKill' or event_type == 'Kill':
        #    kill_str = '{} killed {}'.format(get_client_profile_markdown(origin_client_name, origin_client_id),
        #                                                           get_client_profile_markdown(target_client_name, target_client_id))
        #    killed_field = WebhookField('Kill Information', kill_str)
        #    webhook_item_embed.title = 'Player Killed'
        #    webhook_item_embed.fields.append(killed_field)

        #todo: handle other events
        else:
            continue

         #make sure there's at least one group to notify
        if len(notify_role_ids) > 0:
            # unfortunately only the content can be used to to notify members in groups
            #embed content shows the role but doesn't notify
            webhook_item.content = role_ids_string

        events.append({'item' : webhook_item, 'notify' : should_notify})

    return events

# sends the data to the webhook location
def execute_webhook(data):
    for event in data:
        event_json = event['item'].to_json()
        url = None

        if event['notify']:
            url = discord_webhook_notification_url
        else:
            if len(discord_webhook_information_url)  > 0:
                url = discord_webhook_information_url

        if url :
            response = requests.post(url,
                data=event_json, 
                headers={'Content-type' : 'application/json'})

# grabs new events and executes the webhook fo each valid event
def run():
    failed_count = 1
    print('starting polling for events')
    while True:
        try:
            new_events = get_new_events()
            execute_webhook(new_events)
        except Exception as e:
            print('failed to get new events ({})'.format(failed_count))
            print(e)
            failed_count += 1
        time.sleep(5)

if __name__ == "__main__":
    run()