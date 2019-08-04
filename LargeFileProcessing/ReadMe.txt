This project do the following things:
	1). Create a large file(>800MB) contains Log lines or empty lines or something else.
	2). Load the log items by page(the default page size is 500) as soon as possible.
	3). Print the benchmark of loading performance.
Note: 
	1). Log line has its fixed format of "[LogType] [DateTime] [LogContent]"
	2). [LogType] has its fixed format of "L, W, E, I, C, O"
	3). [DateTime] has its fixed format of "yyyy-MM-dd HH:mm:ss.fff"
	4). [LogContent] can be eny sambol or words or empty char.
	5). Each Log line has a '\r\n' ending.
	6). Writing or Reading text by UTF-8 encoding.
References:
	1. How to generate random string
	https://stackoverflow.com/questions/32932679/using-rngcryptoserviceprovider-to-generate-random-string
	2.