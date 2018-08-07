import requests
import time
import json
import collections

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

    def to_json(self):
        return json.dumps(self, default=lambda o: o.__dict__, sort_keys=True)

def get_client_profile(profile_id):
    return '{}/Client/ProfileAsync/{}'.format(base_url, str(profile_id))

with open('config.json') as json_config_file:
    json_config = json.load(json_config_file)

# this should be an URL to an IP or FQN to an IW4MAdmin instance
# ie http://127.0.0.1 or http://IW4MAdmin.com
base_url = json_config['IW4MAdminUrl']
end_point = '/api/event'
request_url = base_url + end_point
# this should be the full discord webhook url
# ie https://discordapp.com/api/webhooks/<id>/<token>
discord_webhook_url = json_config['DiscordWebhookUrl']
# this should be the numerical id of the discord group
# 12345678912345678
notify_role_ids = json_config['NotifyRoleIds']

def get_new_events():
    events = []
    response = requests.get(request_url)
    data = response.json()
    
    for event in data:
        # commonly used event info items
        event_type = event['eventType']['name']
        server_name = event['ownerEntity']['name']

        webhook_item = WebhookParams()
        webhook_item_embed = WebhookEmbed()

        webhook_item.username = 'IW4MAdmin'
        webhook_item.avatar_url = 'https://raidmax.org/IW4MAdmin/img/iw4adminicon-3.png'
        webhook_item_embed.color = 31436
        webhook_item_embed.url = base_url
        webhook_item_embed.thumbnail = { 'url' : 'https://raidmax.org/IW4MAdmin/img/iw4adminicon-3.png' }
        webhook_item.embeds.append(webhook_item_embed)

        role_ids_string = ''
        for id in notify_role_ids:
            role_ids_string += '\r\n<@&{}>\r\n'.format(id)

        webhook_notifyrole = WebhookField('Notifies',role_ids_string)
    
        if event_type == 'Report':
            origin_client_name = event['originEntity']['name']
            origin_client_id = int(event['originEntity']['id'])

            target_client_name = event['targetEntity']['name']
            target_client_id = int(event['targetEntity']['id'])

            report_reason = event['extraInfo']

            server_field = WebhookField('Server', server_name)
            report_reason_field = WebhookField('Reason', report_reason)
            reported_by_field = WebhookField('By',  '[{}]({})'.format(origin_client_name, get_client_profile(origin_client_id)))
            reported_field = WebhookField('Reported Player', '[{}]({})'.format(target_client_name, get_client_profile(target_client_id)))

            webhook_item_embed.title = 'Player Reported'
            webhook_item_embed.fields.append(server_field)
            webhook_item_embed.fields.append(reported_field)
            webhook_item_embed.fields.append(reported_by_field)
            webhook_item_embed.fields.append(report_reason_field)
            
            #make sure there's at least one group to notify
            if len(notify_role_ids) > 0:
                webhook_item.content = role_ids_string

        else:
            continue

        events.append(webhook_item)

    return events

def execute_webhook(data):
    for event in data:
        event_json = event.to_json()
        response = requests.post(discord_webhook_url, data=event_json, headers={'Content-type' : 'application/json'})

def run():
    while True:
        try:
            new_events = get_new_events()
            execute_webhook(new_events)
        except:
            print('failed to get new events')
        time.sleep(2.5)

if __name__ == "__main__":
    run()