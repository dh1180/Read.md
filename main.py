import os
from dotenv import load_dotenv
from fastapi import FastAPI, HTTPException
from sqlalchemy import create_engine, Column, Integer, String, DateTime, func
from sqlalchemy.ext.declarative import declarative_base
from sqlalchemy.orm import sessionmaker

load_dotenv()
DATABASE_URL = os.getenv("DATABASE_URL")

engine = create_engine(DATABASE_URL)
Session = sessionmaker(bind=engine)
Base = declarative_base()

# 독후감 모델
class Review(Base):
    __tablename__ = "reviews"
    id = Column(Integer, primary_key=True)
    title = Column(String)
    book_title = Column(String)
    content = Column(String)
    author = Column(String)
    created_at = Column(DateTime, default=func.now())
    updated_at = Column(DateTime, default=func.now(), onupdate=func.now())

Base.metadata.create_all(engine)

app = FastAPI()

@app.post("/reviews")
def create_review(title: str, book_title: str, content: str, author: str):
    session = Session()
    review = Review(title=title, book_title=book_title, content=content, author=author)
    session.add(review)
    session.commit()
    session.refresh(review)
    session.close()
    return review

@app.get("/reviews")
def get_reviews():
    session = Session()
    reviews = session.query(Review).all()
    session.close()
    return reviews

@app.get("/reviews/{review_id}")
def get_review_by_id(review_id: int):
    session = Session()
    review = session.query(Review).get(review_id)
    if review is None:
        raise HTTPException(status_code=404, detail="Review not found")
    session.close()
    return review

@app.put("/reviews/{review_id}")
def update_review(review_id: int, title: str, book_title: str, content: str, author: str):
    session = Session()
    review = session.query(Review).get(review_id)
    if review is None:
        raise HTTPException(status_code=404, detail="Review not found")
    review.title = title
    review.book_title = book_title
    review.content = content
    review.author = author
    session.commit()
    session.refresh(review)
    session.close()
    return review