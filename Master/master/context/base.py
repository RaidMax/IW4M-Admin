from apscheduler.schedulers.background import BackgroundScheduler
from apscheduler.triggers.interval import IntervalTrigger
import time

class Base():
    def __init__(self):
        self.instance_list = {}
        self.server_list = {}
        self.token_list = {}
        self.scheduler = BackgroundScheduler()
        self.scheduler.start()
        self.scheduler.add_job(
            func=self._remove_staleinstances,
            trigger=IntervalTrigger(seconds=120),
            id='stale_instance_remover',
            name='Remove stale instances if no heartbeat in 120 seconds',
            replace_existing=True
        )

    def _remove_staleinstances(self):
        for key, value in list(self.instance_list.items()):
            if int(time.time()) - value.last_heartbeat > 120:
                print('[_remove_staleinstances] removing stale instance {id}'.format(id=key))
                del self.instance_list[key]
                del self.token_list[key]
        print('[_remove_staleinstances] {count} active instances'.format(count=len(self.instance_list)))

    def get_server_count(self):
        return self.server_list.count

    def get_instance_count(self):
        return self.instance_list.count

    def get_instance(self, id):
        return self.instance_list[id]

    def instance_exists(self, instance_id):
        if instance_id in self.instance_list.keys():
            return instance_id
        else:
            False

    def add_instance(self, instance):
        if instance.id in self.instance_list:
            print('[add_instance] instance {id} already added, updating instead'.format(id=instance.id))
            return self.update_instance(instance)
        else:
            print('[add_instance] adding instance {id}'.format(id=instance.id))
            self.instance_list[instance.id] = instance

    def update_instance(self, instance):
        if instance.id not in self.instance_list:
            print('[update_instance] instance {id} not added, adding instead'.format(id=instance.id))
            return self.add_instance(instance)
        else:
            print('[update_instance] updating instance {id}'.format(id=instance.id))
            self.instance_list[instance.id] = instance

    def add_token(self, instance_id, token):
        print('[add_token] adding {token} for id {id}'.format(token=token, id=instance_id))
        self.token_list[instance_id] = token

    def get_token(self, instance_id):
        try:
            return self.token_list[instance_id]
        except KeyError:
            return False