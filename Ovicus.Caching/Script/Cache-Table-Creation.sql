
CREATE TABLE [dbo].[Cache](
	[Key] [nvarchar](250) NOT NULL,
	[Value] [varbinary](max) NOT NULL,
	[Created] [datetime] NOT NULL,
	[LastAccess] [datetime] NOT NULL,
	[SlidingExpirationTimeInMinutes] [int] NULL,
	[AbsoluteExpirationTime] [datetime] NULL,
	[ObjectType] [nvarchar](250) NOT NULL
) ON [PRIMARY]



