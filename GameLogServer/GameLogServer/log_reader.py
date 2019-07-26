import re
import os
import time
import random
import string

class LogReader(object):
    def __init__(self):
        self.log_file_sizes = {}
        # (if the time between checks is greater, ignore ) - in seconds
        self.max_file_time_change = 60

    def read_file(self, path, retrieval_key):
        # this removes old entries that are no longer valid
        try:
            self._clear_old_logs()
        except Exception as e:
            print('could not clear old logs')
            print(e)

        if os.name != 'nt':
            path = re.sub(r'^[A-Z]\:', '', path)
            path = re.sub(r'\\+', '/', path)

        # prevent traversing directories
        if re.search('r^.+\.\.\\.+$', path):
            return self._generate_bad_response()

        # must be a valid log path and log file
        if not re.search(r'^.+[\\|\/](.+)[\\|\/].+.log$', path):
            return self._generate_bad_response()

        # get the new file size
        new_file_size = self.file_length(path)
          
        # the log size was unable to be read (probably the wrong path)
        if new_file_size < 0:
            return self._generate_bad_response()

        next_retrieval_key = self._generate_key()

        # this is the first time the key has been requested, so we need to the next one
        if retrieval_key not in self.log_file_sizes:
            print('retrieval key "%s" does not exist' % retrieval_key)
            last_log_info = {
                'size' : new_file_size,
                'previous_key' : None
            }
        else:
            last_log_info = self.log_file_sizes[retrieval_key]

        print('next key is %s' % next_retrieval_key)
        expired_key = last_log_info['previous_key']
        print('expired key is %s' % expired_key)

        # grab the previous value
        last_size = last_log_info['size']
        file_size_difference = new_file_size - last_size

        #print('generating info for next key %s' % next_retrieval_key)

        # update the new size
        self.log_file_sizes[next_retrieval_key] = {
            'size' : new_file_size,
            'read': time.time(),
            'next_key': next_retrieval_key,
            'previous_key': retrieval_key
        }

        if expired_key in self.log_file_sizes:
            print('deleting expired key %s' % expired_key)
            del self.log_file_sizes[expired_key]

        #print('reading %i bytes starting at %i' % (file_size_difference, last_size))

        new_log_content = self.get_file_lines(path, last_size, file_size_difference)
        return {
            'content': new_log_content,
            'next_key': next_retrieval_key
        }

    def get_file_lines(self, path, start_position, length_to_read):
        try:
            file_handle = open(path, 'rb')
            file_handle.seek(start_position)
            file_data = file_handle.read(length_to_read)
            file_handle.close()
            # using ignore errors omits the pesky 0xb2 bytes we're reading in for some reason
            return file_data.decode('utf-8', errors='ignore')
        except Exception as e:
            print('could not read the log file at {0}, wanted to read {1} bytes'.format(path, length_to_read))
            print(e)
            return False

    def _clear_old_logs(self):
        expired_logs = [path for path in self.log_file_sizes if int(time.time() - self.log_file_sizes[path]['read']) > self.max_file_time_change]
        for key in expired_logs:
            print('removing expired log with key {0}'.format(key))
            del self.log_file_sizes[key]

    def _generate_bad_response(self):
        return {
            'content': None,
            'next_key': None
        }

    def _generate_key(self):
        return ''.join(random.choices(string.ascii_uppercase + string.digits, k=8))
        
    def file_length(self, path):
        try:
            return os.stat(path).st_size
        except Exception as e:
            print('could not get the size of the log file at {0}'.format(path))
            print(e)
            return -1

reader = LogReader()
