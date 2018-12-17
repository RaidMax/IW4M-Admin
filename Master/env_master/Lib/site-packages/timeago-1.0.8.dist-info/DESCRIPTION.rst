A very simple python library, used to format datetime with *** time ago statement.

Install

    pip install timeago

Usage

import timeago, datetime
d = datetime.datetime.now() + datetime.timedelta(seconds = 60 * 3.4)
# locale
print (timeago.format(d, locale='zh_CN')) # will print 3分钟后



